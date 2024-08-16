#!/usr/bin/env -S dotnet fsi

#r "nuget: FSharp.Data"

open System.IO
open FSharp.Data

//, Schema="Designator (string), Val (string), Package (string), Mid X (decimal), Mid Y (decimal), Rotation (decimal), Layer (string)"
type KicadPos = CsvProvider<const(__SOURCE_DIRECTORY__ + "/kicad_pos_sample.csv"), HasHeaders=true>

let hasPlaceableValue (row: KicadPos.Row) =
    let ignoredValues =
        Set.ofList
            [ "OLED-I2C"
              "LOGO"
              "TC2030-AVR"
              "MountingHole"
              "breakaway-hole-big"
              "DNF"
              "Rotary_Encoder_Switch" ]

    not (ignoredValues.Contains row.Val)

let hasPlaceableRef (row: KicadPos.Row) =
    let ignoredDesignators = Set.ofList [ "REF**" ]

    not (ignoredDesignators.Contains row.Val)

let flipTopHotswaps (row: KicadPos.Row) =
    match row with
    | r when r.Side = "top" && r.Package.Contains "Hotswap" ->
        KicadPos.Row(r.Ref, r.Val, r.Package, -r.PosX, r.PosY, r.Rot, "bottom")
    | _ -> row

let mirrorX (r: KicadPos.Row) =
    KicadPos.Row(r.Ref, r.Val, r.Package, -r.PosX, r.PosY, r.Rot, r.Side)

let absXY (r: KicadPos.Row) =
    KicadPos.Row(r.Ref, r.Val, r.Package, abs (r.PosX), abs (r.PosY), r.Rot, r.Side)

let absX (r: KicadPos.Row) =
    KicadPos.Row(r.Ref, r.Val, r.Package, abs (r.PosX), r.PosY, r.Rot, r.Side)

let flipRotation (r: KicadPos.Row) =
    KicadPos.Row(r.Ref, r.Val, r.Package, r.PosX, r.PosY, -r.Rot, r.Side)

let rotate (degrees: decimal) (row: KicadPos.Row) =
    let newRot = (row.Rot + degrees) % 360m
    KicadPos.Row(row.Ref, row.Val, row.Package, row.PosX, row.PosY, newRot, row.Side)

let offset (dx: decimal) (dy: decimal) (row: KicadPos.Row) =
    let t = float row.Rot * System.Math.PI / 180.0

    let newX =
        System.Math.Round(row.PosX + dx * decimal (cos t) + dy * decimal (sin t), 6)

    let newY =
        System.Math.Round(row.PosY + -dx * decimal (sin t) + dy * decimal (cos t), 6)

    KicadPos.Row(row.Ref, row.Val, row.Package, newX, newY, row.Rot, row.Side)

let fixRotations (row: KicadPos.Row) =
    match row with
    | r when r.Package.Contains "Hotswap" -> (rotate 180m >> offset 0.635m -3.81m) r
    //| r when r.Package.Contains "HRO-TYPE-C" -> (rotate 180m >> offset 0m 5.0m) r
    | r when r.Package = "USB_C_Receptacle_HRO_TYPE-C-31-M-12" -> (rotate 180m >> offset 0m 1.25m) r
    | r when r.Package = "USB_C_Receptacle_TYPE-C-16P-CB1-6-073" -> (flipRotation >> rotate 180m >> offset 0m -1.8m) r
    | r when r.Package = "SOT-23" -> rotate 180m r
    | r when r.Package = "SOIC-8_5.23mm-USON-8_2x3mm" -> rotate 90m r
    | r when r.Package.Contains "SOIC-8" -> rotate -90m r
    | r when r.Package = "SOT-143B" -> rotate -90m r
    | r when r.Package = "SOT-143" -> rotate 90m r
    | r when r.Package = "SOT-353_SC-70-5" -> rotate 90m r
    | r when r.Package = "QFN-48-1EP_7x7mm_P0.5mm_EP5.6x5.6mm" -> rotate -90m r
    | r when r.Package = "WS2812B" -> rotate 90m r
    | r when r.Package = "RP2040-QFN-56" -> rotate 180m r
    | r when r.Package = "SW_choc_v2_HS_CPG135001S30_1u" -> (flipRotation >> offset 2.5m 4.85m) r
    | r when r.Package = "SW_choc_v1+v2_HS_CPG135001S30_1u" -> (flipRotation >> offset 2.5m 4.85m) r
    | r when r.Package = "SK6812-MINI-E" -> flipRotation r
    //| r when r.Val = "2N7002" -> rotate 180m r
    | _ -> row

let convertPos (inputCsv: string) (outputCsv: string option) =
    let posInput = KicadPos.Load(inputCsv)

    let transformedPos =
        posInput
            .Filter(hasPlaceableValue)
            .Filter(hasPlaceableRef)
            .Map(flipTopHotswaps)
            //.Map(mirrorX)
            //.Map(absXY)
            .Map(absX)
            .Map(fixRotations)

    //printfn "Transformed: %A" (transformedPos.SaveToString())

    let lines =
        transformedPos.SaveToString().Split()
        |> Array.filter (System.String.IsNullOrWhiteSpace >> not)

    let newHeader = "Designator,Val,Package,Mid X,Mid Y,Rotation,Layer"
    Array.set lines 0 newHeader
    printfn "%A" lines

    let outputFile =
        match outputCsv with
        | Some outputCsv -> outputCsv
        | None ->
            let dir = Path.GetDirectoryName(inputCsv)
            let name = Path.GetFileNameWithoutExtension(inputCsv)

            let newName =
                if name.Contains("all-pos") then
                    name.Replace("all-pos", "cpl")
                else
                    name + "-cpl"

            Path.Combine(dir, newName + ".csv")

    File.WriteAllLines(outputFile, lines)

match fsi.CommandLineArgs with
| [| scriptName; inputCsv; outputCsv |] -> convertPos inputCsv (Some outputCsv)
| [| scriptName; inputCsv |] -> convertPos inputCsv None
| _ -> printfn "Usage: ./make_jlc_cpl.fsx [Input CSV] [Output CSV]"
