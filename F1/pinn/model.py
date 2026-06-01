"""
model.py  —  Physics-Informed Neural Network architecture.

Network: fully-connected MLP with periodic / tanh activations.
  Input  : (x, y, z)  — normalised coordinates in [-1, 1]^3
  Output : (u, v, w, p) — velocity components [m/s] + pressure [Pa]
           where u=v=w=0 on the wing surface enforces the no-slip BC.

Why tanh?
  tanh is smooth and bounded, so autograd can compute high-order derivatives
  needed for the Navier-Stokes residual without numerical blow-up.
"""
import torch
import torch.nn as nn


class PINN(nn.Module):
    """
    4-hidden-layer MLP with tanh activations.

    Architecture
    ─────────────
    Input(3) → [Linear(3→64) → tanh] × 4 → Linear(64→4)

    Output vector: [u, v, w, p]
      u, v, w : velocity  (no-slip BC: u=v=w=0 on wing surface)
      p       : pressure  (Cp = 2p / (rho * V_inf^2) derived post-hoc)
    """

    def __init__(self,
                 hidden_size: int = 64,
                 n_layers: int = 4,
                 activation: type[nn.Module] = nn.Tanh):
        super().__init__()

        layers: list[nn.Module] = []

        # Input layer
        layers.append(nn.Linear(3, hidden_size))
        layers.append(activation())

        # Hidden layers
        for _ in range(n_layers - 1):
            layers.append(nn.Linear(hidden_size, hidden_size))
            layers.append(activation())

        # Output layer (no activation — raw outputs)
        layers.append(nn.Linear(hidden_size, 4))

        self.net = nn.Sequential(*layers)

        # Xavier initialisation for tanh networks
        self._init_weights()

    def _init_weights(self):
        for m in self.modules():
            if isinstance(m, nn.Linear):
                nn.init.xavier_normal_(m.weight)
                nn.init.zeros_(m.bias)

    def forward(self, x: torch.Tensor) -> torch.Tensor:
        """
        Parameters
        ----------
        x : (N, 3)  normalised coordinates

        Returns
        -------
        out : (N, 4)  [u, v, w, p]
        """
        return self.net(x)

    def uvwp(self, x: torch.Tensor):
        """Convenience: unpack output into individual velocity + pressure tensors."""
        out = self.forward(x)
        return out[:, 0:1], out[:, 1:2], out[:, 2:3], out[:, 3:4]
