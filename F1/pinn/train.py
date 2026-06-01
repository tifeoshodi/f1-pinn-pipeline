"""
train.py  —  Main entry point for PINN training + inference.

Usage:
    python train.py [options]

Options:
    --data   <path>   boundary_points.json  (default: ../C++/boundary_extractor/output/boundary_points.json)
    --epochs <n>      training epochs        (default: 3000)
    --lr     <f>      learning rate          (default: 1e-3)
    --samples <n>     interior colloc points (default: 10000)
    --out    <dir>    output directory       (default: output/)
    --plot            save loss curve after training
    --help
"""
import argparse
import sys

# Force UTF-8 output on Windows (prevents cp1252 UnicodeEncodeError for any
# Unicode characters printed during training, e.g. from matplotlib or torch).
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
if hasattr(sys.stderr, "reconfigure"):
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

from pathlib import Path

# ── Resolve project root regardless of CWD ───────────────────────────────────
HERE = Path(__file__).resolve().parent
DEFAULT_DATA = HERE / ".." / "C++" / "boundary_extractor" / "output" / "boundary_points.json"
DEFAULT_OUT  = HERE / "output"


def parse_args():
    p = argparse.ArgumentParser(
        description="Train the F1 front wing PINN and run pressure field inference.")
    p.add_argument("--data",    default=str(DEFAULT_DATA), help="boundary_points.json path")
    p.add_argument("--epochs",  type=int,   default=3000,   help="Training epochs")
    p.add_argument("--lr",      type=float, default=1e-3,   help="Initial learning rate")
    p.add_argument("--samples", type=int,   default=10_000, help="Interior collocation points")
    p.add_argument("--out",     default=str(DEFAULT_OUT),   help="Output directory")
    p.add_argument("--plot",    action="store_true",        help="Save loss curve PNG")
    return p.parse_args()


def main():
    args = parse_args()

    out_dir = Path(args.out)
    out_dir.mkdir(parents=True, exist_ok=True)
    weights_path = str(out_dir / "pinn_weights.pt")
    infer_path   = str(out_dir / "pressure_field.json")

    # ── 1. Load data ──────────────────────────────────────────────────────────
    from data import load
    print(f"\n[train] Loading boundary data from:\n        {args.data}")
    dataset = load(args.data, n_interior=args.samples)

    # ── 2. Train ──────────────────────────────────────────────────────────────
    from trainer import train
    model = train(
        dataset,
        n_epochs      = args.epochs,
        lr            = args.lr,
        lambda_bc     = 10.0,
        lambda_pde    = 1.0,
        lambda_p      = 1.0,
        lambda_inlet  = 5.0,
        pde_batch_size= 256,
        hidden_size   = 64,
        n_layers      = 4,
        log_every     = max(1, args.epochs // 30),
        save_path     = weights_path,
        device        = "cpu",
    )

    # ── 3. Inference ──────────────────────────────────────────────────────────
    from infer import run_inference
    run_inference(model, dataset, out_path=infer_path)

    # ── 4. Optional loss plot ─────────────────────────────────────────────────
    if args.plot:
        _plot_loss(out_dir)

    print(f"\n[train] Pipeline complete.")
    print(f"        pressure_field.json  → {infer_path}")


def _plot_loss(out_dir: Path):
    """Plot and save the training loss curve using matplotlib."""
    import json
    import matplotlib
    matplotlib.use("Agg")   # non-interactive backend (safe for any environment)
    import matplotlib.pyplot as plt

    hist_path = out_dir / "loss_history.json"
    if not hist_path.exists():
        print("[plot] No loss_history.json found — skipping plot.")
        return

    with open(hist_path) as f:
        history = json.load(f)

    epochs  = [h["epoch"]  for h in history]
    l_total = [h["loss"]   for h in history]
    l_bc    = [h["l_bc"]   for h in history]
    l_pde   = [h["l_pde"]  for h in history]
    l_p     = [h["l_p"]    for h in history]

    fig, ax = plt.subplots(figsize=(9, 5))
    ax.semilogy(epochs, l_total, label="Total",          color="#e74c3c", lw=2)
    ax.semilogy(epochs, l_bc,    label="L_bc (no-slip)", color="#3498db", lw=1.5, ls="--")
    ax.semilogy(epochs, l_pde,   label="L_pde (NS)",     color="#2ecc71", lw=1.5, ls="--")
    ax.semilogy(epochs, l_p,     label="L_p (gauge)",    color="#f39c12", lw=1.5, ls=":")
    ax.set_xlabel("Epoch")
    ax.set_ylabel("Loss (log scale)")
    ax.set_title("F1 Front Wing PINN — Training Loss")
    ax.legend()
    ax.grid(True, which="both", alpha=0.3)
    fig.tight_layout()

    plot_path = out_dir / "loss_curve.png"
    fig.savefig(plot_path, dpi=150)
    plt.close(fig)
    print(f"[plot] Loss curve saved to {plot_path}")


if __name__ == "__main__":
    main()
