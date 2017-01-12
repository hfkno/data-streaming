
#I "../../packages/"
#r "Microsoft.Hadoop.Avro/lib/net45/Microsoft.Hadoop.Avro.dll"
#r "System.Runtime.Serialization.dll"

open System.Runtime.Serialization;
open System.Data
open Microsoft.Hadoop.Avro
open Microsoft.Hadoop.Avro.Container

[<DataContract>]
type Person = 
    { Name : string
      [<DataMember(Name = "Age")>]
      Age : int }

let serializer = AvroSerializer.Create<Person>()  /// (typeof<Person>)