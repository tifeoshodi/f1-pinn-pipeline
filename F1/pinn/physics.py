"""
physics.py  —  Navier-Stokes PDE residual via autograd.

Governing equations (steady, incompressible, laminar):
  Continuity :  ∂u/∂x + ∂v/∂y + ∂w/∂z = 0
  Momentum x :  u∂u/∂x + v∂u/∂y + w∂u/∂z = -1/ρ ∂p/∂x + ν∇²u
  Momentum y :  u∂v/∂x + v∂v/∂y + w∂v/∂z = -1/ρ ∂p/∂y + ν∇²v
  Momentum z :  u∂w/∂x + v∂w/∂y + w∂w/∂z = -1/ρ ∂p/∂z + ν∇²w

All coordinates are normalised, so all derivatives are w.r.t. normalised coords.
The residual is computed over interior collocation points x_col.

NOTE on physical constants (for reference — residuals are dimensionless here):
  ρ (air density)  ≈ 1.225 kg/m³  at sea level
  ν (kinematic viscosity) ≈ 1.5 × 10⁻⁵ m²/s
  V_inf (free-stream speed) = 50 m/s  (typical slow-speed testing)
  Re = V_inf * c1 / ν ≈ 50 × 0.3 / 1.5e-5 ≈ 1,000,000  (turbulent in reality;
       PINN uses laminar as a first-order physics surrogate)
"""
import torch
import torch.nn as nn
from typing import Callable


def _grad(y: torch.Tensor, x: torch.Tensor) -> torch.Tensor:
    """Compute ∂y/∂x using autograd (requires x.requires_grad=True)."""
    return torch.autograd.grad(
        y, x,
        grad_outputs=torch.ones_like(y),
        create_graph=True,
        retain_graph=True,
    )[0]


def ns_residual(model: nn.Module,
                x_col: torch.Tensor,
                nu: float = 1.5e-5) -> dict[str, torch.Tensor]:
    """
    Compute the 4 Navier-Stokes residuals at interior collocation points.

    Parameters
    ----------
    model : PINN
    x_col : (M, 3)  interior points WITH requires_grad=True
    nu    : kinematic viscosity [m²/s]

    Returns
    -------
    dict with keys 'continuity', 'momentum_x', 'momentum_y', 'momentum_z'
    each of shape (M, 1), mean-squared error is the physics loss component.
    """
    x_col = x_col.requires_grad_(True)
    u, v, w, p = model.uvwp(x_col)

    # ── First-order spatial derivatives ───────────────────────────────────────
    u_x = _grad(u, x_col)[:, 0:1]
    u_y = _grad(u, x_col)[:, 1:2]
    u_z = _grad(u, x_col)[:, 2:3]

    v_x = _grad(v, x_col)[:, 0:1]
    v_y = _grad(v, x_col)[:, 1:2]
    v_z = _grad(v, x_col)[:, 2:3]

    w_x = _grad(w, x_col)[:, 0:1]
    w_y = _grad(w, x_col)[:, 1:2]
    w_z = _grad(w, x_col)[:, 2:3]

    p_x = _grad(p, x_col)[:, 0:1]
    p_y = _grad(p, x_col)[:, 1:2]
    p_z = _grad(p, x_col)[:, 2:3]

    # ── Second-order (Laplacian terms) ────────────────────────────────────────
    u_xx = _grad(u_x, x_col)[:, 0:1]
    u_yy = _grad(u_y, x_col)[:, 1:2]
    u_zz = _grad(u_z, x_col)[:, 2:3]

    v_xx = _grad(v_x, x_col)[:, 0:1]
    v_yy = _grad(v_y, x_col)[:, 1:2]
    v_zz = _grad(v_z, x_col)[:, 2:3]

    w_xx = _grad(w_x, x_col)[:, 0:1]
    w_yy = _grad(w_y, x_col)[:, 1:2]
    w_zz = _grad(w_z, x_col)[:, 2:3]

    # ── Residuals ─────────────────────────────────────────────────────────────
    # Continuity: ∇·u = 0
    r_cont = u_x + v_y + w_z

    # Momentum (inertia + pressure gradient − viscous diffusion = 0)
    r_mom_x = u*u_x + v*u_y + w*u_z + p_x - nu*(u_xx + u_yy + u_zz)
    r_mom_y = u*v_x + v*v_y + w*v_z + p_y - nu*(v_xx + v_yy + v_zz)
    r_mom_z = u*w_x + v*w_y + w*w_z + p_z - nu*(w_xx + w_yy + w_zz)

    return {
        "continuity":  r_cont,
        "momentum_x":  r_mom_x,
        "momentum_y":  r_mom_y,
        "momentum_z":  r_mom_z,
    }


def bc_loss(model: nn.Module, x_bc: torch.Tensor) -> torch.Tensor:
    """
    No-slip boundary condition on the wing surface:
        u(x_bc) = v(x_bc) = w(x_bc) = 0

    Returns mean-squared velocity magnitude at the boundary.
    """
    u, v, w, _ = model.uvwp(x_bc)
    return (u**2 + v**2 + w**2).mean()


def inlet_loss(model: nn.Module,
               x_col: torch.Tensor,
               V_inf: float = 1.0) -> torch.Tensor:
    """
    Far-field / inlet BC: u = V_inf, v = w = 0 at the upstream face.

    Selects collocation points with normalised x < -0.8 (upstream inlet region)
    and enforces free-stream velocity there. This BREAKS the trivial zero solution:
    the network must now produce u→V_inf upstream AND u=0 on the wing surface,
    which forces a physically meaningful flow field in between.

    V_inf = 1.0 in normalised coordinates (actual speed = 50 m/s; see infer.py).
    """
    mask = x_col[:, 0] < -0.8
    if mask.sum() == 0:
        # Fallback: use the 10% most upstream points
        k = max(1, x_col.shape[0] // 10)
        idx = torch.argsort(x_col[:, 0])[:k]
        x_in = x_col[idx].detach()
    else:
        x_in = x_col[mask].detach()

    u, v, w, _ = model.uvwp(x_in)
    return ((u - V_inf)**2 + v**2 + w**2).mean()


def pressure_loss(model: nn.Module,
                  x_bc: torch.Tensor,
                  p_ref: float = 0.0) -> torch.Tensor:
    """
    Soft constraint: pressure at the leading edge stagnation point (x≈-1)
    is approximately the free-stream stagnation pressure p_ref.
    This anchors the pressure field to prevent gauge degeneracy.
    """
    # Select points near the leading edge region (normalised x < -0.8)
    mask = x_bc[:, 0] < -0.8
    if mask.sum() == 0:
        return torch.tensor(0.0, device=x_bc.device)
    _, _, _, p = model.uvwp(x_bc[mask])
    return ((p - p_ref)**2).mean()
