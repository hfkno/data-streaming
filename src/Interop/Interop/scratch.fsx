




#r "../../packages/FSharp.Data/lib/net40/FSharp.Data.dll"
#r "../../packages/FSharp.Data.TypeProviders/lib/net40/FSharp.Data.TypeProviders.dll"



open FSharp.Data
open FSharp.Data.TypeProviders
open FSharp.Linq
open System


// Producer

System.IO.File.Exists("../test/formattest.csv")

type ``Visma Leverandør Data``= CsvProvider<Schema="../test/formattest.csv", HasHeaders= false>

let publishRows (uri:Uri) =
    use suppliers = ``Visma Leverandør Data``.Load("../test/formattest.csv") 
    printfn "%A" (suppliers.Rows |> Seq.length)
    for supplier in suppliers.Rows do printfn "%O" supplier
