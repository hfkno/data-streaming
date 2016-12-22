


#r "../../packages/FSharp.Data/lib/net40/FSharp.Data.dll"
#r "System.Runtime.Serialization"
#r "System.Xml.dll"
#r "System.Xml.Linq.dll"

open System
open System.IO
open System.Linq
open System.Text.RegularExpressions
open FSharp.Data



module Configuration =

    type Authentification = { UserName : string; Password : string; }

    [<AutoOpen>]
    module private Implementation =
        let path = System.IO.Path.Combine [|__SOURCE_DIRECTORY__ ; "sensitive.config" |]

        type Settings = XmlProvider<"sensitive.config">
        let content = Settings.GetSample()

    let ExchangeAdmin = 
        let exadmin = content.Authentification.Exchangeadmin
        { UserName = exadmin.Username; Password = exadmin.Password }








