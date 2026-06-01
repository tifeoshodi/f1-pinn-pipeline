"""
data.py  —  Load boundary_points.json into PyTorch tensors.

Provides two tensor sets:
  boundary  : (N, 3) surface collocation points [mm]  + (N, 3) outward normals
  interior  : (M, 3) random domain points inside a bounding box around the wing
              used for enforcing the PDE residual in free-stream regions

All coordinates are normalised to [-1, 1] using the bounding box of the
boundary points so that the MLP has well-conditioned inputs.
"""
import json
from pathlib import Path
from typing import NamedTuple

import torch


class WingDataset(NamedTuple):
    # Boundary (surface) tensors
    x_bc:  torch.Tensor   # (N, 3) — surface positions,  normalised
    n_bc:  torch.Tensor   # (N, 3) — outward unit normals (unchanged by normalisation)
    # Interior (collocation) tensors
    x_col: torch.Tensor   # (M, 3) — random interior points, normalised
    # Normalisation constants (stored for denormalisation)
    coords_min: torch.Tensor   # (3,)
    coords_max: torch.Tensor   # (3,)
    # Raw metadata dict
    metadata: dict


def load(json_path: str | Path,
         n_interior: int = 10_000,
         interior_margin: float = 1.2,
         seed: int = 0,
         dtype: torch.dtype = torch.float32,
         device: str = "cpu") -> WingDataset:
    """
    Parameters
    ----------
    json_path      : path to boundary_points.json from the C++ extractor
    n_interior     : number of random interior collocation points to sample
    interior_margin: factor to expand the bounding box for interior sampling
                     1.2 = 20% larger in each direction
    seed           : RNG seed for interior point sampling
    dtype / device : tensor config
    """
    with open(json_path) as f:
        data = json.load(f)

    meta  = data["metadata"]
    pts   = torch.tensor(data["points"],  dtype=dtype, device=device)   # (N, 3) mm
    norms = torch.tensor(data["normals"], dtype=dtype, device=device)   # (N, 3)

    # ── Normalisation  ────────────────────────────────────────────────────────
    # Map each axis to [-1, 1] using bounding box from metadata
    bb = meta["bounds"]
    coords_min = torch.tensor([bb["x_min"], bb["y_min"], bb["z_min"]],
                               dtype=dtype, device=device)
    coords_max = torch.tensor([bb["x_max"], bb["y_max"], bb["z_max"]],
                               dtype=dtype, device=device)
    span = coords_max - coords_min                                # (3,)

    def normalise(x: torch.Tensor) -> torch.Tensor:
        return 2.0 * (x - coords_min) / span - 1.0              # maps to [-1, 1]

    x_bc = normalise(pts)                                        # (N, 3)

    # ── Interior collocation points  ──────────────────────────────────────────
    # Sample uniformly inside an expanded bounding box (approximates the flow domain).
    torch.manual_seed(seed)
    lo = -interior_margin * torch.ones(3, dtype=dtype, device=device)
    hi =  interior_margin * torch.ones(3, dtype=dtype, device=device)
    x_col = torch.rand(n_interior, 3, dtype=dtype, device=device) * (hi - lo) + lo

    print(f"[data] Boundary points : {x_bc.shape[0]}")
    print(f"[data] Interior points : {x_col.shape[0]}")
    print(f"[data] Coords range    : [{coords_min.tolist()}, {coords_max.tolist()}] mm")

    return WingDataset(
        x_bc=x_bc,
        n_bc=norms,
        x_col=x_col,
        coords_min=coords_min,
        coords_max=coords_max,
        metadata=meta,
    )
