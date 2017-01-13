
#I "../../packages/"
#r "Microsoft.Hadoop.Avro/lib/net45/Microsoft.Hadoop.Avro.dll"
#r "System.Runtime.Serialization.dll"
#r "Newtonsoft.Json/lib/net45/Newtonsoft.Json.dll"
#r "Newtonsoft.Json.Schema/lib/net45/Newtonsoft.Json.Schema.dll"
#load "FSharp.Formatting/FSharp.Formatting.fsx"
open FSharp.Literate
open System.IO
open System.Runtime.Serialization;
open System.Data
open System.Collections.Generic
open Microsoft.Hadoop.Avro
open Microsoft.Hadoop.Avro.Container
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open Newtonsoft.Json.Schema
open Newtonsoft.Json.Schema.Generation
open Microsoft.FSharp.Reflection



type Address = { Location: string; Code: int }

type Person = 
    { Name : string
      Age : int
      Address : Address }


type PersonSimple = 
    { Name : string
      Age : int }



// reflect a record
// spit out a C# POCO























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
    printfn "%s" (p.Type.ToString())
    printfn "%A"  (p.Value.ToString())
    printfn "%O" (p.Type.GetType())





let jgen = new JSchemaGenerator()
let sch = jgen.Generate(typeof<PersonC>)
let tsc = jgen.Generate(typeof<PersonSimple>)
let ssc = jgen.Generate(typeof<Person>)


let rec listProps indent (props:IDictionary<string, JSchema>) =
    for p in props do
        printfn "%O"p.Key
        printfn "%O"p.Value
        listProps (indent + 1) p.Value.Properties


listProps 0 (ssc.Properties)





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








