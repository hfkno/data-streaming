



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





type IsolatedContractCompiler() =
    inherit MarshalByRefObject()

    member x.CurrAppDomain () = AppDomain.CurrentDomain.FriendlyName

    member x.compile (typeName:string, source:string) =
        let codeProvider = new CSharpCodeProvider()
        let parameters = new CompilerParameters()
        parameters.GenerateExecutable <- false
        parameters.GenerateInMemory <- true
        parameters.ReferencedAssemblies.Add("System.dll") |> ignore
        parameters.ReferencedAssemblies.Add("System.Runtime.Serialization.dll") |> ignore

        let results = codeProvider.CompileAssemblyFromSource(parameters, source)
        if results.Errors.HasErrors then failwith (sprintf "Errors: %s" (results.Errors.ToString()))
        let ass = results.CompiledAssembly
        results.CompiledAssembly, ass.GetType(typeName)



module SchemaGenerator =

    type Schema = 
        { Name : string
          Schema : string }

    [<AutoOpen>]
    module private Utility =


        let fullName (id:string) (t:Type) = sprintf "%s.%s" id t.Name
        let toValType (name:string) = 
            match name with
            | "Boolean" -> "boolean"
            | "String" ->"string"
            | "Int32" -> "int"
            | "Double" -> "double"
            | "DateTime" -> "string"
            | _ -> failwith (sprintf "Unknown type '%s'" name)


        let genClass (id:string) (t:Type) : string * string =

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

            let fields = 
                FSharpType.GetRecordFields t
                |> Seq.map(fun p -> 
                    sprintf "\t\t\t[System.Runtime.Serialization.DataMember]\r\n\t\t\t%s %s {get; set;}\r\n" 
                            (p.PropertyType.Name |> toValType) p.Name )
                |> String.Concat
            (t |> fullName id), (sprintf classTemplate id t.Name fields)

        let isolateAndCompile (typename:string, source:string) =
            // Avoiding memory leaks: http://stackoverflow.com/questions/1799373/how-can-i-prevent-compileassemblyfromsource-from-leaking-memory
            let domain = AppDomain.CreateDomain("TempContractCompilation")
//            let compiler = 
//                domain
//                    .CreateInstanceAndUnwrap(typeof<IsolatedContractCompiler>.Assembly.FullName, "IsolatedContractCompiler")
//                    :?> IsolatedContractCompiler
            let compiler = new IsolatedContractCompiler()
            let ass, t  = compiler.compile(typename,source)    
            domain |> AppDomain.Unload
            typename, t


        let generateMessage (id:string) (t:Type) =
            (genClass id t) |> isolateAndCompile


        let messageSchema<'a>  (typeName:string, t:Type) = // : IAvroSerializer<'a> * Schema.RecordSchema = 
            let serializer = 
                (typeof<AvroSerializer>)
                    .GetMethod("Create", ([||]:Type array))
                    .MakeGenericMethod(t)
                    .Invoke(null, null)

            let schema =
                serializer
                    .GetType()
                    .GetProperty("WriterSchema")
                    .GetValue(serializer, null)
                    :?> Schema.RecordSchema

            { Name = typeName; Schema = schema.ToString() }

        let print schema = schema.ToString()


    let generateSchema<'a> id = typeof<'a> |> (generateMessage id >> messageSchema<'a>)
    let generateSchemaText<'a> = generateSchema<'a> |> print








module SchemaGeneratorTest =

    type Address = { Location: string; Code: int }

    type Person = 
        { Name : string
          Age : int
          Address : Address }

    type PersonSimple = 
        { Name : string
          Age : int 
          Created: DateTime }

    let schema () = SchemaGenerator.generateSchema<PersonSimple> "hfk.utility.test"

    type Three = {Name : string}

    let example () = SchemaGenerator.generateSchema<Three> "hfk.utility.test"










module Examples =

    open SchemaGeneratorTest
    open FSharp.CodeFormat
    open System.Reflection


    let Avro () =
        // AVRO Serializer Example: only works with public setters and getters
        let serializer = AvroSerializer.Create<PersonSimple>()
        printfn "%s" (serializer.WriterSchema.ToString())


    let JSon () =
        // JSON Property extraction after serializtion example
        let json = JsonConvert.SerializeObject({ Name = "OI"; Age = 123; Address = { Location = "Loc"; Code = 456 } })
        let jo = JObject.Parse(json)

        for p in jo.Properties() do
            printfn "%s" p.Name
            printfn "%s" (p.Type.ToString())
            printfn "%A"  (p.Value.ToString())
            printfn "%O" (p.Type.GetType())


    let JSonSchema () =
        // JSON SChema Generator Example
        let jgen = new JSchemaGenerator()
        let tsc = jgen.Generate(typeof<PersonSimple>)
        let ssc = jgen.Generate(typeof<Person>)

        let rec listProps indent (props:IDictionary<string, JSchema>) =
            for p in props do
                printfn "%O"p.Key
                printfn "%O"p.Value
                listProps (indent + 1) p.Value.Properties

        listProps 0 (ssc.Properties)


    let LiterateFSharp () =
        // Literate F# Code Formatting Example - Extract documentation strings for inclusion in avro schemas
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








