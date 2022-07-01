#!/usr/bin/env -S dotnet fsi

#r "nuget: FSharp.Data"

open System.IO
open FSharp.Data

//, Schema="Designator (string), Val (string), Package (string), Mid X (decimal), Mid Y (decimal), Rotation (decimal), Layer (string)"
type KicadPos = CsvProvider<const(__SOURCE_DIRECTORY__ + "/kicad_pos_sample.csv"), HasHeaders=true>

// Filter Designator: REF**
// Filter Val: breakaway, MountingHole, DNF, OLED-I2C
// Package: MX-Hotswap flip to top

let hasPlaceableValue (row: KicadPos.Row) =
    let ignoredValues =
        Set.ofList [ "OLED-I2C"
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
    | _ -> KicadPos.Row(row.Ref, row.Val, row.Package, row.PosX, row.PosY, row.Rot, row.Side)


let convertPos (inputCsv: string) (outputCsv: string) =
    let posInput = KicadPos.Load(inputCsv)

    printfn "Header: %A" posInput.Headers
    printfn "%A" (posInput.Rows |> Seq.length)
    // TODO: Update header

    let transformedPos =
        posInput
            .Filter(hasPlaceableValue)
            .Filter(hasPlaceableRef)
            .Map(flipTopHotswaps)

    printfn "Transformed: %A" (transformedPos.SaveToString())

match fsi.CommandLineArgs with
| [| scriptName; inputCsv; outputCsv |] ->
    printfn "POS File: %s" inputCsv
    convertPos inputCsv outputCsv
| _ -> printfn "Usage: ./make_jlc_cpl.fsx [Input CSV] [Output CSV]"
