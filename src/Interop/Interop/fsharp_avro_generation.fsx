
#I "../../packages/"
#r "Microsoft.Hadoop.Avro/lib/net45/Microsoft.Hadoop.Avro.dll"
#r "System.Runtime.Serialization.dll"
#r "Newtonsoft.Json/lib/net45/Newtonsoft.Json.dll"


open System.Runtime.Serialization;
open System.Data
open Microsoft.Hadoop.Avro
open Microsoft.Hadoop.Avro.Container
open Newtonsoft.Json
open Newtonsoft.Json.Linq




type Person = 
    { Name : string
      Age : int }

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





let json = JsonConvert.SerializeObject({ Name = "OI"; Age = 123 })

let jo = JObject.Parse(json)

for p in jo.Properties() do
    printfn "%s" p.Name
    printfn "%A"  (p.Value.ToString())










