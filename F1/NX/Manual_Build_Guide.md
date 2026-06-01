# F1 Front Wing — Manual NX Build Guide
## (NX Student Edition — UI Workflow)

> Use this guide if both journals fail with "package does not support this operation".
> The point files from `preprocess_airfoils.py` are already correct — no re-processing needed.
> Expressions are already in the part from the first journal run.

---

## Pre-check: Confirm Expressions Exist

`Tools → Expressions` — you should see these 15 names already:

| Name | Value | Unit |
|---|---|---|
| c1 | 300 | mm |
| c2 | 180 | mm |
| c3 | 120 | mm |
| alpha1 | 3 | deg |
| alpha2 | 18 | deg |
| alpha3 | 28 | deg |
| g12 | 15 | mm |
| g23 | 12 | mm |
| ov12 | 10 | mm |
| ov23 | 8 | mm |
| b | 950 | mm |
| RH | 60 | mm |
| h_ep | 380 | mm |
| t_ep | 4 | mm |
| ep_fillet_r | 5 | mm |

If missing → add them manually via `Tools → Expressions → New`.

---

## Part 1 — Main Plane (NACA 63-412, c1 = 300 mm)

### 1A. Import the Airfoil Curve

1. **Insert → Curve → Studio Spline** (or `Insert → Curve → Spline`)
2. In the dialog: select **"Through Points"** as the type
3. Click **"Import Points from File"** (or the file icon)
4. Navigate to:
   ```
   F1\NX\Airfoil_Points\main_NACA63412_c300mm.dat
   ```
5. Ensure **Closed** / **Periodic** is checked (the profile is a closed loop)
6. Click **OK** → a closed airfoil curve appears at the origin

> The imported curve is already scaled to 300 mm chord in the XZ plane (Y=0).
> Upper surface is positive Z, leading edge is at the origin.

### 1B. Lift to Ride Height (RH = 60 mm)

1. Select the spline just created
2. **Edit → Move Object** (or `Ctrl+T` → Transform)
3. **Translate** → **Delta XYZ**: X=0, Y=0, **Z=60** (= RH)
4. **Copy Original: No** → OK

### 1C. Apply Angle of Attack (alpha1 = 3°)

1. Select the spline
2. **Edit → Move Object → Rotate**
3. **Axis**: Y-axis (Vector 0,1,0)
4. **Point on axis**: (0, 0, 60) — the leading edge world position
5. **Angle**: **−3°** (negative = nose-down = downforce)
6. **Copy Original: No** → OK

### 1D. Extrude (Span = b = 950 mm)

1. **Insert → Design Feature → Extrude** (or press `X`)
2. **Section**: click the airfoil spline
3. **Direction**: +Y axis (0, 1, 0)
4. **Start**: 0 mm | **End**: type `b` (references the expression)
5. **Boolean**: None (create new body)
6. OK → solid main plane body created

---

## Part 2 — Flap 1 (NACA 4412, c2 = 180 mm)

**Slot geometry:**
- Flap 1 LE position = **(c1 − ov12, 0, RH + g12) = (290, 0, 75) mm**

### 2A. Import Flap 1 Curve

Same as 1A, but select:
```
F1\NX\Airfoil_Points\flap1_NACA4412_c180mm.dat
```

### 2B. Translate to Slot Position

**Edit → Move Object → Translate → Delta XYZ:**
- X = **290** mm  (= c1 − ov12 = 300 − 10)
- Y = **0**
- Z = **75** mm   (= RH + g12 = 60 + 15)

### 2C. Apply AoA (alpha2 = 18°)

**Edit → Move Object → Rotate**
- Axis: Y (0,1,0)
- Point on axis: (290, 0, 75) — flap 1 leading edge world position
- Angle: **−18°**

### 2D. Extrude

Same as 1D — select flap 1 spline, direction +Y, End = `b`.

---

## Part 3 — Flap 2 (NACA 4412, c3 = 120 mm)

**Slot geometry:**
- Flap 2 LE = **(f1X + c2 − ov23, 0, f1Z + g23) = (462, 0, 87) mm**

### 3A. Import Flap 2 Curve

```
F1\NX\Airfoil_Points\flap2_NACA4412_c120mm.dat
```

### 3B. Translate

Delta XYZ: X=**462**, Y=0, Z=**87**

### 3C. Apply AoA (alpha3 = 28°)

Rotate about (462, 0, 87), Y-axis, Angle=**−28°**

### 3D. Extrude

Select flap 2 spline → +Y → End = `b`.

---

## Part 4 — Endplate

### 4A. Create Sketch at Span Tip

1. **Insert → Sketch** → choose **Plane Method = At Distance**
2. Select the **YZ plane** or define by point (0, 950, 0) with normal (0,1,0)
3. Draw a **Rectangle**:
   - Bottom-left: X = −20, Z = 60
   - Width: = (c1 + c2 + c3) + 40 ≈ **640 mm** (chord extent)
   - Height: = **h_ep = 380 mm**
4. Finish Sketch

### 4B. Extrude Endplate

1. Select the rectangle sketch
2. Direction: **−Y** (0, −1, 0) — inboard direction
3. End = `t_ep` (= 4 mm)
4. Boolean: **None**
5. OK

### 4C. Fillet Top Edge (Optional)

1. **Insert → Detail Feature → Edge Blend**
2. Select top edge of endplate
3. Radius = `ep_fillet_r` (= 5 mm)

---

## Part 5 — Final Checks

1. **Analysis → Model → Geometry Check** → expect 0 errors
2. **File → Export → STL** → save for Docker/PINN pipeline verification
3. **File → Export → STEP (AP214)** → save for C++ boundary extractor (Task 2)

---

## Slot Geometry Reference Table

| Parameter | Expression | Value | Description |
|---|---|---|---|
| Main LE | — | (0, 0, 60) mm | Fixed at datum + RH |
| Flap 1 LE | c1−ov12 / RH+g12 | (290, 0, 75) mm | Gap + Overlap from main |
| Flap 2 LE | f1X+c2−ov23 / f1Z+g23 | (462, 0, 87) mm | Gap + Overlap from flap 1 |
| Endplate Y | b | Y = 950 mm | Span tip |

---

## Troubleshooting

| Problem | Fix |
|---|---|
| Spline imports as open curve | Check "Periodic/Closed" in spline dialog |
| Profile is inside-out (suction surface down) | Reverse spline direction: Edit → Curve → Reverse |
| Extrude says "no solid body created" | Profile must be closed (no gap at LE/TE) |
| Flap intersects main plane | Check ov12 value; increase g12 if needed |
