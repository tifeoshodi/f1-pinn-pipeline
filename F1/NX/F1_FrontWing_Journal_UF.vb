' =============================================================================
' F1_FrontWing_Journal_UF.vb
' Siemens NX Open Journal — 3-Element F1 Front Wing (UF API version)
'
' This version uses UFSession.Modl functions instead of Feature Builders
' to work around NX Student Edition license restrictions on NXOpen.Features.
'
' Run via: Tools -> Journal -> Play
' Pre-requisite: Run preprocess_airfoils.py first (Airfoil_Points\ must exist)
'
' COORDINATE CONVENTION:
'   X = chord direction (LE -> TE)
'   Y = span direction  (root = 0, tip = b)
'   Z = thickness       (positive = suction/upper surface)
' =============================================================================

Option Strict Off
Imports System
Imports System.IO
Imports System.Collections.Generic
Imports NXOpen
Imports NXOpen.UF

Module F1FrontWingJournal_UF

    Dim theSession   As Session
    Dim workPart     As Part
    Dim theUF        As UFSession
    Dim lw           As ListingWindow

    ' ── Wing parameters (defaults; overridden by SyncFromExpressions) ─────────
    Dim c1      As Double = 300.0
    Dim c2      As Double = 180.0
    Dim c3      As Double = 120.0
    Dim alpha1  As Double = 3.0
    Dim alpha2  As Double = 18.0
    Dim alpha3  As Double = 28.0
    Dim g12     As Double = 15.0
    Dim g23     As Double = 12.0
    Dim ov12    As Double = 10.0
    Dim ov23    As Double = 8.0
    Dim bSpan   As Double = 950.0
    Dim RH      As Double = 60.0
    Dim h_ep    As Double = 380.0
    Dim t_ep    As Double = 4.0

    Dim datDir As String =
        "C:\Users\user\Downloads\Tife's F1 Career Study\F1\NX\Airfoil_Points"

    ' =========================================================================
    Sub Main()
        theSession = Session.GetSession()
        workPart   = theSession.Parts.Work
        theUF      = UFSession.GetUFSession()
        lw         = theSession.ListingWindow
        lw.Open()
        lw.WriteLine("== F1 Front Wing (UF API) — START ==")

        ' Validate directory
        If Not Directory.Exists(datDir) Then
            lw.WriteLine("ERROR: Missing Airfoil_Points\ directory.")
            lw.WriteLine("       Run preprocess_airfoils.py first.")
            Return
        End If

        ' ── 1. Expressions (skip if already exist from previous run) ──────────
        CreateExpressions()
        SyncFromExpressions()
        lw.WriteLine("[1] Expressions ready")
        lw.WriteLine("    c1=" & c1 & " c2=" & c2 & " c3=" & c3 & " [mm]")
        lw.WriteLine("    a1=" & alpha1 & " a2=" & alpha2 & " a3=" & alpha3 & " [deg]")
        lw.WriteLine("    g12=" & g12 & " g23=" & g23 & " ov12=" & ov12 & " ov23=" & ov23)
        lw.WriteLine("    b=" & bSpan & " RH=" & RH)

        ' ── 2. Main Plane — LE at (0, 0, RH), AoA = alpha1 (applied as -alpha1) ─
        lw.WriteLine("[2] Building Main Plane...")
        Dim mainOK As Boolean = BuildElement(FindDat("main"), c1, bSpan, -alpha1,
                                             0.0, 0.0, RH, "Main_Plane")
        If mainOK Then lw.WriteLine("    Main Plane: OK") Else lw.WriteLine("    Main Plane: FAILED — see error above")

        ' ── 3. Flap 1 — LE at (c1-ov12, 0, RH+g12) ───────────────────────────
        Dim f1X As Double = c1 - ov12
        Dim f1Z As Double = RH + g12
        lw.WriteLine("[3] Building Flap 1...")
        Dim f1OK As Boolean = BuildElement(FindDat("flap1"), c2, bSpan, -alpha2,
                                           0.0, f1X, f1Z, "Flap_1")
        If f1OK Then lw.WriteLine("    Flap 1: OK  LE=(" & f1X & "," & f1Z & ")")

        ' ── 4. Flap 2 — LE at (f1X+c2-ov23, 0, f1Z+g23) ─────────────────────
        Dim f2X As Double = f1X + c2 - ov23
        Dim f2Z As Double = f1Z + g23
        lw.WriteLine("[4] Building Flap 2...")
        Dim f2OK As Boolean = BuildElement(FindDat("flap2"), c3, bSpan, -alpha3,
                                           0.0, f2X, f2Z, "Flap_2")
        If f2OK Then lw.WriteLine("    Flap 2: OK  LE=(" & f2X & "," & f2Z & ")")

        ' ── 5. Endplate ───────────────────────────────────────────────────────
        lw.WriteLine("[5] Building Endplate...")
        Dim epOK As Boolean = BuildEndplate(-(20.0), (f2X + c3) + 20.0)
        If epOK Then lw.WriteLine("    Endplate: OK")

        workPart.Save(BasePart.SaveComponents.True, BasePart.CloseAfterSave.False)
        lw.WriteLine("== F1 Front Wing (UF API) — COMPLETE ==")
    End Sub

    ' =========================================================================
    '  EXPRESSIONS
    ' =========================================================================
    Sub CreateExpressions()
        Dim defs() As String = {
            "c1","300", "c2","180", "c3","120",
            "alpha1","3", "alpha2","18", "alpha3","28",
            "g12","15", "g23","12", "ov12","10", "ov23","8",
            "b","950", "RH","60", "h_ep","380", "t_ep","4", "ep_fillet_r","5"
        }
        Dim i As Integer
        For i = 0 To defs.Length - 1 Step 2
            Try
                workPart.Expressions.CreateExpression("Number", defs(i) & "=" & defs(i+1))
            Catch : End Try
        Next i
    End Sub

    Sub SyncFromExpressions()
        Dim e As Expression
        For Each e In workPart.Expressions
            Select Case e.Name
                Case "c1"    : c1    = e.Value
                Case "c2"    : c2    = e.Value
                Case "c3"    : c3    = e.Value
                Case "alpha1": alpha1 = e.Value
                Case "alpha2": alpha2 = e.Value
                Case "alpha3": alpha3 = e.Value
                Case "g12"   : g12   = e.Value
                Case "g23"   : g23   = e.Value
                Case "ov12"  : ov12  = e.Value
                Case "ov23"  : ov23  = e.Value
                Case "b"     : bSpan = e.Value
                Case "RH"    : RH    = e.Value
                Case "h_ep"  : h_ep  = e.Value
                Case "t_ep"  : t_ep  = e.Value
            End Select
        Next
    End Sub

    Function FindDat(prefix As String) As String
        Dim files() As String = Directory.GetFiles(datDir, prefix & "_*.dat")
        If files.Length = 0 Then
            Throw New Exception("No .dat found for '" & prefix & "' in " & datDir)
        End If
        Return files(0)
    End Function

    ' =========================================================================
    '  BUILD ELEMENT — UF API version
    '  Uses UFSession.Modl.CreateSplineThruPts  +  UFSession.Modl.CreateExtruded
    ' =========================================================================
    Function BuildElement(datFile As String, chord As Double, span As Double,
                          aoaDeg As Double, leY As Double,
                          leX As Double, leZ As Double,
                          name As String) As Boolean
        Try
            ' ── 1. Read point file ─────────────────────────────────────────────
            Dim localPts As New List(Of Point3d)
            Dim lines() As String = File.ReadAllLines(datFile)
            Dim line As String
            For Each line In lines
                Dim tr As String = line.Trim()
                If tr.StartsWith("!") OrElse String.IsNullOrWhiteSpace(tr) Then Continue For
                Dim parts() As String = tr.Split(
                    New Char(){" "c, Chr(9)}, StringSplitOptions.RemoveEmptyEntries)
                If parts.Length >= 2 Then
                    localPts.Add(New Point3d(
                        Double.Parse(parts(0)),   ' X  (chord)
                        0.0,                       ' Y  (span = 0 at root)
                        Double.Parse(parts(1))))   ' Z  (thickness)
                End If
            Next line

            If localPts.Count < 6 Then
                lw.WriteLine("    ERROR [" & name & "]: too few points (" & localPts.Count & ")")
                Return False
            End If

            ' ── 2. Rotate by AoA about LE (local origin), then translate ───────
            Dim aoaRad As Double = aoaDeg * Math.PI / 180.0
            Dim cosA As Double = Math.Cos(aoaRad)
            Dim sinA As Double = Math.Sin(aoaRad)

            Dim nPts As Integer = localPts.Count
            Dim ptMatrix(nPts - 1, 2) As Double   ' [n, xyz] for UF

            Dim i As Integer
            For i = 0 To nPts - 1
                Dim px As Double = localPts(i).X
                Dim pz As Double = localPts(i).Z
                ' Rotate in XZ plane about Y axis
                Dim rx As Double = px * cosA - pz * sinA
                Dim rz As Double = px * sinA + pz * cosA
                ptMatrix(i, 0) = rx + leX
                ptMatrix(i, 1) = leY
                ptMatrix(i, 2) = rz + leZ
            Next i

            ' ── 3. Create closed spline through points — UF API ─────────────────
            ' UFSession.Modl.CreateSplineThruPts(
            '   num_pts, point_array[n,3], params(Nothing=auto), periodic(1=closed), out tag)
            Dim splineTag As NXOpen.Tag = NXOpen.Tag.Null
            theUF.Modl.CreateSplineThruPts(nPts, ptMatrix, Nothing, 1, splineTag)

            If splineTag = NXOpen.Tag.Null Then
                lw.WriteLine("    ERROR [" & name & "]: spline tag is null")
                Return False
            End If
            lw.WriteLine("    Spline OK  (" & nPts & " pts)  tag=" & splineTag.ToString())

            ' ── 4. Extrude along +Y (span direction) using UF API ───────────────
            ' UFSession.Modl.CreateExtruded(
            '   curves[], num_curves, direction[3], limits[2],
            '   offsets[2], tol[3], body_type(0=solid), out body_tag)
            Dim curveTags(0) As NXOpen.Tag
            curveTags(0) = splineTag

            Dim extDir(2) As Double
            extDir(0) = 0.0 : extDir(1) = 1.0 : extDir(2) = 0.0  ' +Y

            Dim limits(1) As Double
            limits(0) = 0.0 : limits(1) = span

            Dim offsets(1) As Double
            offsets(0) = 0.0 : offsets(1) = 0.0

            Dim tol(2) As Double
            tol(0) = 0.01 : tol(1) = 0.5 : tol(2) = 0.01

            Dim bodyTag As NXOpen.Tag = NXOpen.Tag.Null
            theUF.Modl.CreateExtruded(curveTags, 1, extDir, limits, offsets, tol, 0, bodyTag)

            If bodyTag = NXOpen.Tag.Null Then
                lw.WriteLine("    ERROR [" & name & "]: extrude body tag is null")
                Return False
            End If

            ' ── 5. Name the body ─────────────────────────────────────────────────
            Dim bodyObj As NXOpen.Body = CType(
                theSession.GetObjectManager().GetTaggedObject(bodyTag), NXOpen.Body)
            If bodyObj IsNot Nothing Then bodyObj.SetName(name)

            Return True

        Catch ex As Exception
            lw.WriteLine("    EXCEPTION [" & name & "]: " & ex.Message)
            Return False
        End Try
    End Function

    ' =========================================================================
    '  BUILD ENDPLATE — UF API version
    ' =========================================================================
    Function BuildEndplate(epLeX As Double, epTeX As Double) As Boolean
        Try
            ' Endplate rectangle corner points at Y = bSpan
            ' Create 4 lines, then extrude inward (-Y) by t_ep

            ' Lines at Y = bSpan (span tip face)
            Dim p1(2) As Double : p1(0)=epLeX : p1(1)=bSpan : p1(2)=RH
            Dim p2(2) As Double : p2(0)=epTeX : p2(1)=bSpan : p2(2)=RH
            Dim p3(2) As Double : p3(0)=epTeX : p3(1)=bSpan : p3(2)=RH+h_ep
            Dim p4(2) As Double : p4(0)=epLeX : p4(1)=bSpan : p4(2)=RH+h_ep

            Dim l1Tag As NXOpen.Tag, l2Tag As NXOpen.Tag
            Dim l3Tag As NXOpen.Tag, l4Tag As NXOpen.Tag
            theUF.Curve.CreateLine(p1, p2, l1Tag)
            theUF.Curve.CreateLine(p2, p3, l2Tag)
            theUF.Curve.CreateLine(p3, p4, l3Tag)
            theUF.Curve.CreateLine(p4, p1, l4Tag)

            ' Extrude inward (-Y) by t_ep
            Dim curveTags(3) As NXOpen.Tag
            curveTags(0) = l1Tag : curveTags(1) = l2Tag
            curveTags(2) = l3Tag : curveTags(3) = l4Tag

            Dim extDir(2) As Double
            extDir(0) = 0.0 : extDir(1) = -1.0 : extDir(2) = 0.0  ' -Y (inboard)

            Dim limits(1) As Double
            limits(0) = 0.0 : limits(1) = t_ep

            Dim offsets(1) As Double
            offsets(0) = 0.0 : offsets(1) = 0.0

            Dim tol(2) As Double
            tol(0) = 0.01 : tol(1) = 0.5 : tol(2) = 0.01

            Dim bodyTag As NXOpen.Tag = NXOpen.Tag.Null
            theUF.Modl.CreateExtruded(curveTags, 4, extDir, limits, offsets, tol, 0, bodyTag)

            If bodyTag <> NXOpen.Tag.Null Then
                Dim bodyObj As NXOpen.Body = CType(
                    theSession.GetObjectManager().GetTaggedObject(bodyTag), NXOpen.Body)
                If bodyObj IsNot Nothing Then bodyObj.SetName("Endplate")
            End If

            Return bodyTag <> NXOpen.Tag.Null

        Catch ex As Exception
            lw.WriteLine("    EXCEPTION [Endplate]: " & ex.Message)
            Return False
        End Try
    End Function

End Module
