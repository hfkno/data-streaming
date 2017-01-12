
#I "../../packages/"
#r "Microsoft.Hadoop.Avro/lib/net45/Microsoft.Hadoop.Avro.dll"
#r "System.Runtime.Serialization.dll"
#r "Newtonsoft.Json/lib/net45/Newtonsoft.Json.dll"
#load "FSharp.Formatting/FSharp.Formatting.fsx"
open FSharp.Literate
open System.IO


open System.Runtime.Serialization;
open System.Data
open Microsoft.Hadoop.Avro
open Microsoft.Hadoop.Avro.Container
open Newtonsoft.Json
open Newtonsoft.Json.Linq



type Address = { Location: string; Code: int }

type Person = 
    { Name : string
      Age : int
      Address : Address }

[<DataContract>]
type PersonC() =
    let mutable name : string = null
    let mutable age : int = 0
    member x.Name
        with get() = name
        and set(c) = name <- c
    [<DataMember(Name = "Age")>]
    member x.Age
        with get() = age
        and set(a) = age <- a


let serializer = AvroSerializer.Create<PersonC>()   // only wants to serializepublic getters n such...
                                                    // I want records...
printfn "%s" (serializer.WriterSchema.ToString())





let json = JsonConvert.SerializeObject({ Name = "OI"; Age = 123; Address = { Location = "Loc"; Code = 456 } })

let jo = JObject.Parse(json)

for p in jo.Properties() do
    printfn "%s" p.Name
    printfn "%A"  (p.Value.ToString())



open FSharp.CodeFormat
open System.Reflection

let formattingAgent = CodeFormat.CreateAgent()
let source = """
    /// This is the cocumentation
    let hello () = 
      // Normal content
      printfn "Hello world"
  """
let snippets, errors = formattingAgent.ParseSource("C:\\snippet.fsx", source)

// Get the first snippet and obtain list of lines
let (Snippet(title, lines)) = snippets |> Seq.head


let show lines =
    // Iterate over all lines and all tokens on each line
    for (Line(tokens)) in lines do
      for token in tokens do
        match token with
        | TokenSpan.Token(kind, code, tip) -> 
            printf "%s" code
            tip |> Option.iter (fun spans ->
              printfn "%A" spans)          
        | TokenSpan.Omitted _ 
        | TokenSpan.Output _ 
        | TokenSpan.Error _ -> ()
      printfn ""


show lines








