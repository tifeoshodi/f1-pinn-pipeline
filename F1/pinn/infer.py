"""
infer.py  —  Run inference on the trained PINN over the wing surface.

Reads the trained model weights, evaluates (u, v, w, p) at every boundary
point, derives the pressure coefficient Cp, and writes pressure_field.json.

Pressure coefficient:
    Cp = (p - p_inf) / (0.5 * rho * V_inf^2)

where:
    p_inf   = free-stream static pressure  [Pa]   (gauge = 0)
    rho     = air density                  [kg/m³] = 1.225
    V_inf   = free-stream speed            [m/s]   = 50.0

NOTE: the PINN outputs dimensionless-scaled pressures (trained on normalised
coords). The Cp derivation here is a physical interpretation for reporting.
The raw model output `p` is treated as proportional to the true pressure.
"""
import json
from pathlib import Path

import torch

from model import PINN
from data import WingDataset


def run_inference(model:    PINN,
                  dataset:  WingDataset,
                  out_path: str = "output/pressure_field.json",
                  rho:      float = 1.225,
                  V_inf:    float = 50.0,
                  device:   str   = "cpu") -> dict:
    """
    Parameters
    ----------
    model    : trained PINN (in eval mode)
    dataset  : WingDataset (provides x_bc, n_bc, metadata)
    out_path : path for the output JSON file
    rho      : air density  [kg/m³]
    V_inf    : free-stream velocity  [m/s]
    device   : 'cpu' or 'cuda'

    Returns
    -------
    result dict (also written to out_path)
    """
    model.eval()
    x_bc = dataset.x_bc.to(device)
    n_bc = dataset.n_bc.to(device)

    with torch.no_grad():
        u, v, w, p = model.uvwp(x_bc)

    # ── Denormalise coordinates back to mm ────────────────────────────────────
    coords_min = dataset.coords_min.to(device)
    coords_max = dataset.coords_max.to(device)
    span = coords_max - coords_min
    x_mm = ((x_bc + 1.0) / 2.0) * span + coords_min   # (N, 3) in mm

    # -- Pressure coefficient (isentropic definition) -------------------------
    # Cp = 1 - (|V_local| / V_inf)^2
    # V_inf = 1.0 in normalised coords (mapped from 50 m/s free-stream)
    V_inf_norm = 1.0
    vel_sq  = (u**2 + v**2 + w**2).squeeze(-1)       # (N,)
    Cp      = 1.0 - vel_sq / (V_inf_norm**2)          # +1 at stagnation, <0 on suction

    # -- Velocity magnitude ----------------------------------------------------
    vel_mag = torch.sqrt(vel_sq)                       # (N,)

    # ── Assemble output ───────────────────────────────────────────────────────
    pts_mm_list = x_mm.cpu().tolist()
    nrm_list    = n_bc.cpu().tolist()
    Cp_list     = Cp.cpu().tolist()
    vel_list    = vel_mag.cpu().tolist()
    p_list      = p.squeeze(-1).cpu().tolist()
    u_list      = u.squeeze(-1).cpu().tolist()
    v_list      = v.squeeze(-1).cpu().tolist()
    w_list      = w.squeeze(-1).cpu().tolist()

    result = {
        "metadata": {
            "source":      dataset.metadata.get("source_file", "unknown"),
            "n_points":    len(pts_mm_list),
            "V_inf_norm":  1.0,
            "V_inf_ms":    V_inf,
            "rho_kgm3":    rho,
            "q_inf_Pa":    0.5 * rho * V_inf**2,
            "Cp_formula":  "1 - (|V_local|/V_inf_norm)^2  (isentropic)",
            "Cp_min":      float(Cp.min()),
            "Cp_max":      float(Cp.max()),
            "Cp_mean":     float(Cp.mean()),
            "vel_mag_max": float(vel_mag.max()),
            "wing_parameters": dataset.metadata.get("wing_parameters", {}),
        },
        "points_mm":  pts_mm_list,
        "normals":    nrm_list,
        "Cp":         Cp_list,
        "p_raw":      p_list,
        "vel_mag":    vel_list,
        "u":          u_list,
        "v":          v_list,
        "w":          w_list,
    }

    # ── Write JSON ────────────────────────────────────────────────────────────
    out = Path(out_path)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(result, indent=2))

    print(f"\n[infer] Pressure field written to {out_path}")
    print(f"[infer] n_points  : {len(pts_mm_list)}")
    print(f"[infer] Cp range  : [{result['metadata']['Cp_min']:.4f}, "
          f"{result['metadata']['Cp_max']:.4f}]")
    print(f"[infer] Cp mean   : {result['metadata']['Cp_mean']:.4f}")
    print(f"[infer] Vel max   : {result['metadata']['vel_mag_max']:.4f}")

    return result
