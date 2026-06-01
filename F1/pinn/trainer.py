"""
trainer.py  —  PINN training loop.

Loss composition
────────────────
  L_total = λ_bc * L_bc + λ_pde * L_pde + λ_p * L_pressure

  L_bc       : no-slip BC  — MSE of (u²+v²+w²) on wing surface
  L_pde      : NS residual — MSE of continuity + 3 momentum residuals
  L_pressure : gauge fix   — anchors pressure at leading-edge stagnation point

The λ weights start equal and can be tuned in train.py.
Training uses Adam with cosine-annealing LR schedule.
"""
import time
from pathlib import Path

import torch
import torch.nn as nn
import torch.optim as optim

from model import PINN
from physics import bc_loss, ns_residual, pressure_loss, inlet_loss
from data import WingDataset


def train(dataset: WingDataset,
          *,
          n_epochs:      int   = 3000,
          lr:            float = 1e-3,
          lambda_bc:     float = 10.0,
          lambda_pde:    float = 1.0,
          lambda_p:      float = 1.0,
          lambda_inlet:  float = 5.0,    # weight on inlet free-stream BC
          pde_batch_size: int  = 256,
          hidden_size:   int   = 64,
          n_layers:      int   = 4,
          log_every:     int   = 100,
          save_path:     str   = "output/pinn_weights.pt",
          device:        str   = "cpu") -> PINN:
    """
    Train the PINN and return the trained model.

    Parameters
    ----------
    dataset     : WingDataset from data.load()
    n_epochs    : number of Adam optimisation steps
    lr          : initial learning rate
    lambda_bc   : BC loss weight  (higher = stricter no-slip enforcement)
    lambda_pde  : PDE loss weight
    lambda_p    : pressure gauge loss weight
    hidden_size : neurons per hidden layer
    n_layers    : number of hidden layers
    log_every   : print loss every N epochs
    save_path   : where to write the final model weights (.pt)
    device      : 'cpu' or 'cuda'
    """
    model = PINN(hidden_size=hidden_size, n_layers=n_layers).to(device)

    x_bc  = dataset.x_bc.to(device)
    n_bc  = dataset.n_bc.to(device)
    x_col = dataset.x_col.to(device).requires_grad_(True)

    optimizer = optim.Adam(model.parameters(), lr=lr)
    scheduler = optim.lr_scheduler.CosineAnnealingLR(optimizer, T_max=n_epochs, eta_min=1e-5)
    rng = torch.Generator(device=device)
    rng.manual_seed(0)

    # ── Logging ──────────────────────────────────────────────────────────
    history: list[dict] = []
    t0 = time.time()
    n_col = x_col.shape[0]
    bs    = min(pde_batch_size, n_col)

    print(f"\n{'='*56}")
    print(f"  PINN Training  |  epochs={n_epochs}  lr={lr}  device={device}")
    print(f"  Model params   :  {sum(p.numel() for p in model.parameters()):,}")
    print(f"  BC points      :  {x_bc.shape[0]}")
    print(f"  Colloc pool    :  {n_col}  (batch/epoch={bs})")
    print(f"  Loss weights   :  lam_bc={lambda_bc}  lam_pde={lambda_pde}  lam_p={lambda_p}  lam_inlet={lambda_inlet}")
    print(f"{'='*56}\n")
    print(f"{'Epoch':>7}  {'L_total':>10}  {'L_bc':>10}  {'L_pde':>10}  {'L_p':>10}  {'L_in':>10}  {'LR':>9}")
    print("-" * 78)

    best_loss = float("inf")

    for epoch in range(1, n_epochs + 1):
        model.train()
        optimizer.zero_grad()

        # ── Boundary condition loss ──────────────────────────────────────────────
        l_bc = bc_loss(model, x_bc)

        # ── PDE residual loss (mini-batch of interior pts) ──────────────────────
        idx = torch.randperm(n_col, generator=rng)[:bs]
        x_batch = x_col[idx].detach().requires_grad_(True)
        residuals = ns_residual(model, x_batch)
        l_pde = (
            residuals["continuity"].pow(2).mean() +
            residuals["momentum_x"].pow(2).mean() +
            residuals["momentum_y"].pow(2).mean() +
            residuals["momentum_z"].pow(2).mean()
        )

        # -- Inlet / far-field BC loss ------------------------------------------
        l_inlet = inlet_loss(model, x_col)

        # -- Pressure gauge loss --------------------------------------------------
        l_p = pressure_loss(model, x_bc)

        # -- Total loss -----------------------------------------------------------
        loss = (lambda_bc * l_bc + lambda_pde * l_pde
                + lambda_p * l_p + lambda_inlet * l_inlet)
        loss.backward()
        torch.nn.utils.clip_grad_norm_(model.parameters(), max_norm=1.0)
        optimizer.step()
        scheduler.step()

        # Track best model
        if loss.item() < best_loss:
            best_loss = loss.item()
            torch.save(model.state_dict(), save_path)

        if epoch % log_every == 0 or epoch == 1:
            lr_now = scheduler.get_last_lr()[0]
            print(f"{epoch:>7}  {loss.item():>10.6f}  {l_bc.item():>10.6f}"
                  f"  {l_pde.item():>10.6f}  {l_p.item():>10.6f}"
                  f"  {l_inlet.item():>10.6f}  {lr_now:>9.2e}")
            history.append({
                "epoch":   epoch,
                "loss":    loss.item(),
                "l_bc":    l_bc.item(),
                "l_pde":   l_pde.item(),
                "l_p":     l_p.item(),
                "l_inlet": l_inlet.item(),
            })

    elapsed = time.time() - t0
    print(f"\n{'='*56}")
    print(f"  Training complete in {elapsed:.1f}s")
    print(f"  Best total loss : {best_loss:.6f}")
    print(f"  Weights saved   : {save_path}")
    print(f"{'='*56}\n")

    # Save loss history as JSON for plotting
    import json
    hist_path = Path(save_path).with_name("loss_history.json")
    hist_path.write_text(json.dumps(history, indent=2))
    print(f"  Loss history    : {hist_path}")

    # Load the best checkpoint before returning
    model.load_state_dict(torch.load(save_path, map_location=device))
    model.eval()
    return model
