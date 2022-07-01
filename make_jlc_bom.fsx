#!/usr/bin/env -S dotnet fsi

#r "nuget: FSharp.Data"

open System.IO
open FSharp.Data

type KicadBom = CsvProvider<const(__SOURCE_DIRECTORY__ + "/kicad_bom_sample.csv"), HasHeaders=true>

let filterRows rows : seq<KicadBom.Row> =
    let ignored =
        Set.ofList [ "breakaway-hole-big"
                     "RotaryEncoder_EC11"
                     "I2C_Breakout" ]

    rows
    |> Seq.filter (fun row -> not (ignored.Contains row.Package))


let generateBom (inputCsv: string) (outputCsv: string) =
    let bomInput = KicadBom.Load(inputCsv)
    printfn "Header: %A" bomInput.Headers
    printfn "%A" bomInput.Rows

    let filteredRows = filterRows bomInput.Rows
    printfn "%A" filteredRows

// TODO: Rename headers

// TODO: Add LCSC Part numbers



match fsi.CommandLineArgs with
| [| scriptName; inputCsv; outputCsv |] ->
    printfn "BOM File: %s" inputCsv
    generateBom inputCsv outputCsv
| _ -> printfn "Usage: ./make_jlc.bom.fsx [Input CSV] [Output CSV]"
