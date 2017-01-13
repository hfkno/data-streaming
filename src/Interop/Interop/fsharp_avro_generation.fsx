

#I "../../packages/"
#r "Microsoft.Hadoop.Avro/lib/net45/Microsoft.Hadoop.Avro.dll"
#r "System.Runtime.Serialization.dll"
#r "Newtonsoft.Json/lib/net45/Newtonsoft.Json.dll"
#r "Newtonsoft.Json.Schema/lib/net45/Newtonsoft.Json.Schema.dll"
#load "FSharp.Formatting/FSharp.Formatting.fsx"
open FSharp.Literate
open System
open System.Reflection
open System.IO
open System.Runtime.Serialization;
open System.Data
open System.Linq
open System.Collections.Generic
open Microsoft.Hadoop.Avro
open Microsoft.Hadoop.Avro.Container
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open Newtonsoft.Json.Schema
open Newtonsoft.Json.Schema.Generation
open Microsoft.FSharp.Reflection
open System.CodeDom.Compiler
open Microsoft.CSharp

type Address = { Location: string; Code: int }

type Person = 
    { Name : string
      Age : int
      Address : Address }


type PersonSimple = 
    { Name : string
      Age : int }




module SchemaGenerator =

    [<AutoOpen>]
    module private Utility =

        let nameSpace (t:Type) = if t.Namespace = null then "hfk" else t.Namespace
        let fullName (t:Type) = sprintf "%s.%s" (t |> nameSpace) t.Name
        let toValType (name:string) = 
            match name with
            | "String" ->"string"
            | "Int32" -> "int"
            | _ -> failwith (sprintf "Unknown type '%s'" name)

    
        let genClass (t:Type) : string * string =

            let classTemplate : Printf.StringFormat<string -> string -> string -> string> =
                """
            namespace %s {
                [System.Runtime.Serialization.DataContract]
                public class %s
                {
        %s
                }
            }
                """

            let ns = t |> nameSpace

            let fields = 
                FSharpType.GetRecordFields t
                |> Seq.map(fun p -> 
                    sprintf "\t\t\t[System.Runtime.Serialization.DataMember]\r\n\t\t\t%s %s {get; set;}\r\n" 
                            (p.PropertyType.Name |> toValType) p.Name )
                |> String.Concat
    
            (t |> fullName), (sprintf classTemplate ns t.Name fields)

        let generateMessage (t:Type) =
            let typeName, source = genClass t
            let codeProvider = new CSharpCodeProvider()
            let parameters = new CompilerParameters()
            parameters.GenerateExecutable <- false
            parameters.GenerateInMemory <- true
            parameters.ReferencedAssemblies.Add("System.dll") |> ignore
            parameters.ReferencedAssemblies.Add("System.Runtime.Serialization.dll") |> ignore

            let results = codeProvider.CompileAssemblyFromSource(parameters, source)
            if results.Errors.HasErrors then failwith (sprintf "Errors: %s" (results.Errors.ToString()))
            let ass = results.CompiledAssembly
            ass.GetType(typeName)

        let messageSchema (t:Type) = 
            let serializer = 
                (typeof<AvroSerializer>)
                    .GetMethod("Create", ([||]:Type array))
                    .MakeGenericMethod(t)
                    .Invoke(null, null)

            serializer
                .GetType()
                .GetProperty("WriterSchema")
                .GetValue(serializer, null)
                .ToString()


    let generateSchema<'a> = typeof<'a> |> (generateMessage >> messageSchema)

SchemaGenerator.generateSchema<PersonSimple>






[<System.Runtime.Serialization.DataContract>]
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

[<DataContract>]
type Test =
    [<DataMember>]
    member x.Name
        with get() = ""
        and set(c:string) = () //<- c
    [<DataMember>]
    member x.Age
        with get() = 5
        and set(a:int) = ()
    
let tser = AvroSerializer.Create<Test>()  // it seems like DataContract is neccesary... not sure why though
tser.WriterSchema.ToString()
(typeof<Test>).GetTypeInfo().GetConstructor(Type.EmptyTypes) <> null







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








