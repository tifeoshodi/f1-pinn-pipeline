' =============================================================================
' F1_FrontWing_Journal.vb
' Siemens NX Open Journal  —  3-Element Parametric F1 Front Wing
' =============================================================================
'
' HOW TO RUN
'   1. Open Siemens NX → New → Model → Metric (mm, Newton)
'   2. Save the empty part as:  F1_FrontWing_Parametric.prt
'   3. Tools → Journal → Play → select THIS file
'
' PRE-REQUISITE
'   Run preprocess_airfoils.py first.  It generates the three .dat point
'   files consumed below inside F1\NX\Airfoil_Points\.
'
' COORDINATE CONVENTION
'   X  =  chord direction  (Leading Edge → Trailing Edge)
'   Y  =  span direction   (root = 0, tip = b)
'   Z  =  thickness        (positive = suction / upper surface)
'
' PARAMETERISATION
'   All 15 design variables are written as named NX Expressions.
'   To change a parameter:  Tools → Expressions → edit value → Update.
'   The journal can also be re-run after editing the module-level defaults
'   below; expressions are created fresh only if they do not already exist.
' =============================================================================

Option Strict Off
Imports System
Imports System.IO
Imports System.Collections.Generic
Imports NXOpen
Imports NXOpen.UF

Module F1FrontWingJournal

    ' ── NX session handles ────────────────────────────────────────────────────
    Dim theSession  As Session
    Dim workPart    As Part
    Dim lw          As ListingWindow

    ' ── Default parameter values  (synced from NX Expressions at runtime) ────
    ' Chord lengths [mm]
    Dim c1      As Double = 300.0
    Dim c2      As Double = 180.0
    Dim c3      As Double = 120.0
    ' Angle of attack [deg]  positive = nose-up
    ' The journal applies these as NOSE-DOWN (negative rotation) to generate downforce
    Dim alpha1  As Double = 3.0
    Dim alpha2  As Double = 18.0
    Dim alpha3  As Double = 28.0
    ' Slot geometry [mm]
    Dim g12     As Double = 15.0    ' Gap  main  → flap 1
    Dim g23     As Double = 12.0    ' Gap  flap1 → flap 2
    Dim ov12    As Double = 10.0    ' Overlap main  → flap 1
    Dim ov23    As Double = 8.0     ' Overlap flap1 → flap 2
    ' Planform [mm]
    Dim bSpan   As Double = 950.0   ' Half-span
    Dim RH      As Double = 60.0    ' Ride height (main LE above datum ground)
    ' Endplate [mm]
    Dim h_ep    As Double = 380.0
    Dim t_ep    As Double = 4.0
    Dim ep_fil  As Double = 5.0

    ' ── Directory containing preprocessed .dat point files ───────────────────
    Dim datDir As String =
        "C:\Users\user\Downloads\Tife's F1 Career Study\F1\NX\Airfoil_Points"


    ' =========================================================================
    '  ENTRY POINT
    ' =========================================================================
    Sub Main()
        theSession = Session.GetSession()
        workPart   = theSession.Parts.Work
        lw         = theSession.ListingWindow
        lw.Open()
        lw.WriteLine("=========================================")
        lw.WriteLine(" F1 Front Wing Journal  —  START")
        lw.WriteLine("=========================================")

        ' ── Validate point-file directory ─────────────────────────────────────
        If Not Directory.Exists(datDir) Then
            lw.WriteLine("ERROR: Airfoil_Points directory not found:")
            lw.WriteLine("       " & datDir)
            lw.WriteLine("       Run preprocess_airfoils.py first.")
            Return
        End If

        ' ── Step 1 : Expressions ──────────────────────────────────────────────
        CreateExpressions()
        SyncFromExpressions()
        lw.WriteLine("[1/6] Expressions created & synced")
        LogParams()

        ' ── Step 2 : Datum CSYS at world origin ───────────────────────────────
        workPart.Datums.CreateFixedDatumCsys()
        lw.WriteLine("[2/6] Datum CSYS created at origin")

        ' ── Step 3 : Main Plane ───────────────────────────────────────────────
        '    LE at (0, 0, RH)  |  AoA = alpha1 (nose-down = -alpha1)
        Dim mainFile As String = FindDat("main")
        BuildElement(mainFile, c1, bSpan, -alpha1, 0.0, 0.0, RH, "Main_Plane")
        lw.WriteLine("[3/6] Main Plane  built  c=" & c1 & "mm  AoA=" & alpha1 & "deg")

        ' ── Step 4 : Flap 1 ───────────────────────────────────────────────────
        '    Slot geometry: g12 and ov12 drive the LE position
        '    LE_X = c1 - ov12   (overlap pushes LE back)
        '    LE_Z = RH + g12    (gap lifts LE above main plane suction surface)
        Dim f1X As Double = c1 - ov12
        Dim f1Z As Double = RH + g12
        Dim f1File As String = FindDat("flap1")
        BuildElement(f1File, c2, bSpan, -alpha2, 0.0, f1X, f1Z, "Flap_1")
        lw.WriteLine("[4/6] Flap 1  built  c=" & c2 & "mm  AoA=" & alpha2 & _
                     "deg  LE=(" & f1X & "," & f1Z & ")")

        ' ── Step 5 : Flap 2 ───────────────────────────────────────────────────
        Dim f2X As Double = f1X + c2 - ov23
        Dim f2Z As Double = f1Z + g23
        Dim f2File As String = FindDat("flap2")
        BuildElement(f2File, c3, bSpan, -alpha3, 0.0, f2X, f2Z, "Flap_2")
        lw.WriteLine("[5/6] Flap 2  built  c=" & c3 & "mm  AoA=" & alpha3 & _
                     "deg  LE=(" & f2X & "," & f2Z & ")")

        ' ── Step 6 : Endplate ─────────────────────────────────────────────────
        '    Mounts at Y = bSpan (span tip), extends from just forward of
        '    main plane LE to just aft of flap 2 TE.
        Dim epLeX As Double = -20.0
        Dim epTeX As Double = (f2X + c3) + 20.0
        BuildEndplate(epLeX, epTeX)
        lw.WriteLine("[6/6] Endplate  built  h=" & h_ep & "mm  t=" & t_ep & "mm")

        ' ── Save ──────────────────────────────────────────────────────────────
        workPart.Save(BasePart.SaveComponents.True, BasePart.CloseAfterSave.False)
        lw.WriteLine("=========================================")
        lw.WriteLine(" F1 Front Wing Journal  —  COMPLETE")
        lw.WriteLine(" Part saved.")
        lw.WriteLine("=========================================")
    End Sub


    ' =========================================================================
    '  EXPRESSION MANAGEMENT
    ' =========================================================================

    Sub CreateExpressions()
        ' Format: name, default_value_string
        Dim defs() As String = {
            "c1",         "300",
            "c2",         "180",
            "c3",         "120",
            "alpha1",     "3",
            "alpha2",     "18",
            "alpha3",     "28",
            "g12",        "15",
            "g23",        "12",
            "ov12",       "10",
            "ov23",       "8",
            "b",          "950",
            "RH",         "60",
            "h_ep",       "380",
            "t_ep",       "4",
            "ep_fillet_r","5"
        }
        Dim i As Integer
        For i = 0 To defs.Length - 1 Step 2
            Try
                workPart.Expressions.CreateExpression("Number",
                                                       defs(i) & "=" & defs(i + 1))
            Catch
                ' Expression already exists — leave existing value intact
            End Try
        Next i
    End Sub

    Sub SyncFromExpressions()
        Dim e As Expression
        For Each e In workPart.Expressions
            Select Case e.Name
                Case "c1"          : c1     = e.Value
                Case "c2"          : c2     = e.Value
                Case "c3"          : c3     = e.Value
                Case "alpha1"      : alpha1 = e.Value
                Case "alpha2"      : alpha2 = e.Value
                Case "alpha3"      : alpha3 = e.Value
                Case "g12"         : g12    = e.Value
                Case "g23"         : g23    = e.Value
                Case "ov12"        : ov12   = e.Value
                Case "ov23"        : ov23   = e.Value
                Case "b"           : bSpan  = e.Value
                Case "RH"          : RH     = e.Value
                Case "h_ep"        : h_ep   = e.Value
                Case "t_ep"        : t_ep   = e.Value
                Case "ep_fillet_r" : ep_fil = e.Value
            End Select
        Next
    End Sub

    Sub LogParams()
        lw.WriteLine("  Chords  : c1=" & c1 & "  c2=" & c2 & "  c3=" & c3 & " [mm]")
        lw.WriteLine("  AoA     : α1=" & alpha1 & "  α2=" & alpha2 & "  α3=" & alpha3 & " [deg]")
        lw.WriteLine("  Gap     : g12=" & g12 & "  g23=" & g23 & " [mm]")
        lw.WriteLine("  Overlap : ov12=" & ov12 & "  ov23=" & ov23 & " [mm]")
        lw.WriteLine("  Planform: b=" & bSpan & "  RH=" & RH & " [mm]")
    End Sub


    ' =========================================================================
    '  FILE HELPER
    ' =========================================================================

    ''' <summary>
    ''' Finds the first .dat file whose name starts with prefix_ in datDir.
    ''' E.g. FindDat("flap1") → "flap1_NACA4412_c180mm.dat"
    ''' </summary>
    Function FindDat(prefix As String) As String
        Dim files() As String = Directory.GetFiles(datDir, prefix & "_*.dat")
        If files.Length = 0 Then
            Throw New Exception("No .dat file found for prefix '" & prefix &
                                "' in " & datDir & Chr(10) &
                                "Run preprocess_airfoils.py first.")
        End If
        Return files(0)
    End Function


    ' =========================================================================
    '  BUILD ELEMENT
    '  Reads the preprocessed NX .dat file, applies AoA rotation about the
    '  leading edge, translates to world position, creates a closed periodic
    '  Studio Spline, then extrudes along +Y for the full half-span.
    ' =========================================================================

    Sub BuildElement(datFile As String,
                     chord   As Double,
                     span    As Double,
                     aoaDeg  As Double,
                     leY     As Double,
                     leX     As Double,
                     leZ     As Double,
                     name    As String)

        ' ── 1. Read point file ────────────────────────────────────────────────
        '   File columns: X_chord_mm   Z_thickness_mm   Y_span_mm(=0)
        Dim localPts As New List(Of Point3d)
        Dim lines() As String = File.ReadAllLines(datFile)
        Dim line As String
        For Each line In lines
            Dim trimmed As String = line.Trim()
            If trimmed.StartsWith("!") OrElse String.IsNullOrWhiteSpace(trimmed) Then
                Continue For
            End If
            Dim parts() As String = trimmed.Split(
                New Char() {" "c, Chr(9)},
                StringSplitOptions.RemoveEmptyEntries)
            If parts.Length >= 3 Then
                ' File: col0=X_chord  col1=Z_thickness  col2=Y_span(0)
                Dim px As Double = Double.Parse(parts(0))   ' NX X  (chord)
                Dim pz As Double = Double.Parse(parts(1))   ' NX Z  (thickness)
                localPts.Add(New Point3d(px, 0.0, pz))
            End If
        Next line

        If localPts.Count < 6 Then
            Throw New Exception("[" & name & "] Too few points (" &
                                localPts.Count & ") in " & datFile)
        End If

        ' ── 2. Rotate about LE (local origin 0,0,0) then translate ───────────
        '   Rotation in XZ plane about +Y axis.
        '   Positive aoaDeg → nose-up;  pass -alpha for downforce.
        Dim aoaRad As Double = aoaDeg * Math.PI / 180.0
        Dim cosA   As Double = Math.Cos(aoaRad)
        Dim sinA   As Double = Math.Sin(aoaRad)

        Dim worldPts(localPts.Count - 1) As Point3d
        Dim i As Integer
        For i = 0 To localPts.Count - 1
            Dim px As Double = localPts(i).X
            Dim pz As Double = localPts(i).Z
            ' Rotate in XZ: X' = X cosA - Z sinA,  Z' = X sinA + Z cosA
            Dim rx As Double = px * cosA - pz * sinA
            Dim rz As Double = px * sinA + pz * cosA
            worldPts(i) = New Point3d(rx + leX, leY, rz + leZ)
        Next i

        ' ── 3. Create Studio Spline (periodic, degree-3, through-points) ──────
        Dim studio As NXOpen.Features.StudioSplineBuilderEx =
            workPart.Features.CreateStudioSplineBuilderEx(NXOpen.NXObject.Null)

        studio.Degree           = 3
        studio.IsPeriodic       = True
        studio.IsAssociative    = True
        studio.Type             = NXOpen.Features.StudioSplineBuilderEx.Types.ThroughPoints
        studio.MatchKnotsType   = NXOpen.Features.StudioSplineBuilderEx.MatchKnotsTypes.CubicTemplate
        studio.InputCurveOption = NXOpen.Features.StudioSplineBuilderEx.InputCurveOptions.Retain

        Dim wpt As Point3d
        For Each wpt In worldPts
            Dim gcData As NXOpen.Features.GeometricConstraintData =
                studio.ConstraintManager.CreateGeometricConstraintData()
            gcData.Point = workPart.Points.CreatePoint(wpt)
            studio.ConstraintManager.Append(gcData)
        Next wpt

        Dim splineFeat As NXOpen.Features.Feature = studio.CommitFeature()
        studio.Dispose()

        ' ── 4. Extract the spline curve from the feature ──────────────────────
        Dim splineCurve As NXOpen.IBaseCurve = Nothing
        Dim ent As NXObject
        For Each ent In splineFeat.GetEntities()
            If TypeOf ent Is NXOpen.IBaseCurve Then
                splineCurve = CType(ent, NXOpen.IBaseCurve)
                Exit For
            End If
        Next ent

        If splineCurve Is Nothing Then
            Throw New Exception("[" & name & "] Could not extract curve from Studio Spline feature")
        End If

        ' ── 5. Extrude along +Y (span) from 0 to b ───────────────────────────
        Dim extBuilder As NXOpen.Features.ExtrudeBuilder =
            workPart.Features.CreateExtrudeBuilder(NXOpen.NXObject.Null)

        Dim spanDir As NXOpen.Direction =
            workPart.Directions.CreateDirection(
                New Point3d(leX, 0.0, leZ),
                New Vector3d(0.0, 1.0, 0.0),
                SmartObject.UpdateOption.WithinModeling)

        extBuilder.Direction = spanDir
        extBuilder.Limits.StartExtend.Value.RightHandSide = "0"
        extBuilder.Limits.EndExtend.Value.RightHandSide   = "b"
        extBuilder.FeatureOptions.BodyType =
            NXOpen.Features.FeatureOptions.BodyStyle.Solid

        ' Build the section from the spline feature
        Dim sect As NXOpen.Section =
            workPart.Sections.CreateSection(0.0095, 0.001, 0.05)
        sect.SetAllowedEntityTypes(NXOpen.Section.AllowTypes.OnlyCurves)

        Dim rules(0) As NXOpen.SelectionIntentRule
        rules(0) = workPart.ScRuleFactory.CreateRuleFeatureCurves(splineFeat)

        ' Help point: mid-chord, at root (Y=0)
        Dim helpPt As New Point3d(leX + chord * 0.5, leY, leZ)
        sect.AddToSection(rules, splineCurve, Nothing, Nothing,
                          helpPt, NXOpen.Section.Mode.Create, False)
        extBuilder.Section = sect

        Dim extFeat As NXOpen.Features.Feature = extBuilder.CommitFeature()
        extBuilder.Dispose()

        ' ── 6. Name the solid body ────────────────────────────────────────────
        Dim bd As Body
        For Each bd In extFeat.GetBodies()
            bd.SetName(name)
        Next bd
    End Sub


    ' =========================================================================
    '  BUILD ENDPLATE
    '  Creates a flat rectangular solid at span tip (Y = b).
    '  Extruded inward (−Y) by t_ep mm.
    '  Height = h_ep mm, running from Z = RH to Z = RH + h_ep.
    '  Chord-wise extent set by caller (epLeX to epTeX).
    ' =========================================================================

    Sub BuildEndplate(epLeX As Double, epTeX As Double)

        ' Corner points of the endplate rectangle (at Y = bSpan)
        Dim p1 As New Point3d(epLeX, bSpan, RH)
        Dim p2 As New Point3d(epTeX, bSpan, RH)
        Dim p3 As New Point3d(epTeX, bSpan, RH + h_ep)
        Dim p4 As New Point3d(epLeX, bSpan, RH + h_ep)

        ' Create a sketch on a plane at Y = bSpan
        Dim skBuilder As NXOpen.SketchInPlaceBuilder =
            workPart.Sketches.CreateSketchInPlaceBuilder2(Nothing)

        Dim skPlane As NXOpen.Plane =
            workPart.Planes.CreatePlane(
                New Point3d(0.0, bSpan, 0.0),
                New Vector3d(0.0, 1.0, 0.0),
                SmartObject.UpdateOption.WithinModeling)

        skBuilder.PlaneReference = skPlane
        Dim epSketch As NXOpen.Sketch = CType(skBuilder.Commit(), NXOpen.Sketch)
        skBuilder.Dispose()
        epSketch.Activate(NXOpen.Sketch.ViewReorient.TrueValue)

        ' Draw the four boundary lines
        Dim l1 As NXOpen.Line = workPart.Curves.CreateLine(p1, p2)
        Dim l2 As NXOpen.Line = workPart.Curves.CreateLine(p2, p3)
        Dim l3 As NXOpen.Line = workPart.Curves.CreateLine(p3, p4)
        Dim l4 As NXOpen.Line = workPart.Curves.CreateLine(p4, p1)

        epSketch.AddGeometry(l1, NXOpen.Sketch.InferConstraintsOption.InferNoConstraints)
        epSketch.AddGeometry(l2, NXOpen.Sketch.InferConstraintsOption.InferNoConstraints)
        epSketch.AddGeometry(l3, NXOpen.Sketch.InferConstraintsOption.InferNoConstraints)
        epSketch.AddGeometry(l4, NXOpen.Sketch.InferConstraintsOption.InferNoConstraints)

        epSketch.Deactivate(NXOpen.Sketch.ViewReorient.TrueValue,
                            NXOpen.Sketch.UpdateLevel.Model)

        ' Extrude inward (−Y) by t_ep
        Dim epExt As NXOpen.Features.ExtrudeBuilder =
            workPart.Features.CreateExtrudeBuilder(NXOpen.NXObject.Null)

        Dim inboardDir As NXOpen.Direction =
            workPart.Directions.CreateDirection(
                New Point3d(0.0, bSpan, 0.0),
                New Vector3d(0.0, -1.0, 0.0),
                SmartObject.UpdateOption.WithinModeling)

        epExt.Direction = inboardDir
        epExt.Limits.StartExtend.Value.RightHandSide = "0"
        epExt.Limits.EndExtend.Value.RightHandSide   = "t_ep"
        epExt.FeatureOptions.BodyType =
            NXOpen.Features.FeatureOptions.BodyStyle.Solid

        Dim epSect As NXOpen.Section =
            workPart.Sections.CreateSection(0.0095, 0.001, 0.05)
        epSect.SetAllowedEntityTypes(NXOpen.Section.AllowTypes.OnlyCurves)

        Dim epRules(0) As NXOpen.SelectionIntentRule
        epRules(0) = workPart.ScRuleFactory.CreateRuleFeatureCurves(epSketch)

        Dim epHelp As New Point3d((epLeX + epTeX) * 0.5, bSpan, RH + h_ep * 0.5)
        epSect.AddToSection(epRules, l1, Nothing, Nothing,
                            epHelp, NXOpen.Section.Mode.Create, False)
        epExt.Section = epSect

        Dim epFeat As NXOpen.Features.Feature = epExt.CommitFeature()
        epExt.Dispose()

        ' Name the solid body
        Dim bd As Body
        For Each bd In epFeat.GetBodies()
            bd.SetName("Endplate")
        Next bd

        ' ── NOTE: Endplate fillet ─────────────────────────────────────────────
        '   To add a ep_fillet_r radius fillet on the top edge of the endplate,
        '   use Insert → Detail Feature → Edge Blend after the journal completes,
        '   or record a journal step while applying it and paste here.
        '   Expression "ep_fillet_r" is available with value = 5 mm.
    End Sub

End Module
