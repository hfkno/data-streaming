

#r "System.DirectoryServices"
#r "System.DirectoryServices.AccountManagement"
#r "System.Linq"
#r "System.Xml.Linq.dll"
#r "../../packages/FSharp.Data/lib/net40/FSharp.Data.dll"
open System
open System.DirectoryServices
open System.DirectoryServices.AccountManagement
open System.Linq
open System.Net
open System.Collections.Generic
open FSharp.Data
open FSharp.Data.HttpRequestHeaders


let makeUser firstname middlename lastname username email password =
    use pc = new PrincipalContext(ContextType.Domain, "ad.hfk.no", "OU=HFK,DC=ad,DC=hfk,DC=no")
    use up = new UserPrincipal(pc)
    up.Name <- firstname
    up.MiddleName <- middlename
    up.GivenName <- lastname
    up.SamAccountName <- username
    up.EmailAddress <- email
    up.SetPassword(password)
    up.ExpirePasswordNow()
    up.Save()

[<Literal>]
let ``User Creation CSV Schema`` = "Firstname (string), Middlename (string), Lastname (string), Username (string), email (string), password (string)"
type ``User Creation Data``= CsvProvider<Schema= ``User Creation CSV Schema``, Separators = ",", HasHeaders = true, Sample="data/usercreation_sample.csv">


let users = ``User Creation Data``.Load(__SOURCE_DIRECTORY__ + "/data/usercreation_sample.csv")
for r in users.Rows do
    printfn "Creating user %s" r.Username
    makeUser r.Firstname r.Middlename r.Lastname r.Username r.Email r.Password



for arg in fsi.CommandLineArgs do
    printfn "%s" arg


