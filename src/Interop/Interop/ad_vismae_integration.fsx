﻿
(**
Active Directoy -> Visma Enterprise Integration
===============================================


Integration overview
-------------------

  * Retreive users from Active Directory
  * Retreive users from Visma Enterprise
  * Synch changes from active directory with Visma user data
  * Push user changes to Visma Enterprise through their user management webservice

Integration details
-------------------
Initial connection to LDAP for user retrieval
*)
(*** include: list-users ***)

(**
Implementation
--------------
### Active Directory
Active Directory is searched using the managed API
*)
(*** include: ad-operations ***)



(*** hide ***)

#r "System.DirectoryServices"
#r "System.DirectoryServices.AccountManagement"
#r "System.Linq"
#r "System.Xml.Linq.dll"
#r "../../packages/FSharp.Data/lib/net40/FSharp.Data.dll"
open System
open System.DirectoryServices
open System.DirectoryServices.AccountManagement
open System.Linq
open System.Collections.Generic
open FSharp.Data
open FSharp.Data.HttpRequestHeaders





(*** define: utility ***)

let union a b = (a:IEnumerable<'a>).Union(b)


(*** define: ad-operations ***)

/// Active Directory operations and management
module ActiveDirectory =

    /// AD User account information
    type User = 
      { EmployeeId : string
        DisplayName : string
        Account : string
        Email : string
        IsActive : bool
        Other : string }
    with 
        static member Default = 
          { EmployeeId = "-1"
            DisplayName = "Unknown"
            Account = ""
            Email = "unknown@example.com"
            IsActive = false
            Other = "" }

    /// Determines if a user account is active or not at the current moment
    let private isActive (user : UserPrincipal)  = 
        not <| (user.AccountExpirationDate.HasValue && user.AccountExpirationDate.Value < DateTime.Now)

    /// EmployeeIds are stored as the carLicense property with a birthdate suffixed
    let private employeeId (user: UserPrincipal) =
        try
            let de = user.GetUnderlyingObject() :?> DirectoryEntry
            let licenseProp = de.Properties.["carLicense"]
            if licenseProp.Value |> isNull then
                User.Default.EmployeeId
            else
                let license = licenseProp.Value.ToString()
                let birthDateLength = 6
                license.Substring(0, license.Length - birthDateLength)
        with
        | _ -> 
            failwith (sprintf "Error reading employeeId for user '%s' (%s)" user.DisplayName user.SamAccountName)

    /// Yields domain users that match the provided pattern
    let private findUsersMatching (pattern) =
        seq {
            use context = new PrincipalContext(ContextType.Domain, "ad.hfk.no", "OU=HFK,DC=ad,DC=hfk,DC=no")
            use userSearch = new UserPrincipal(context)
            userSearch.GivenName <- pattern
            use search = new PrincipalSearcher(userSearch)

            for principal in search.FindAll() do
                let user = (principal :?> UserPrincipal)

                yield { EmployeeId = user |> employeeId
                        DisplayName = user.DisplayName
                        Email = user.EmailAddress
                        Account = user.SamAccountName
                        IsActive = user |> isActive
                        Other = "" } }

    /// Yields all users
    let users () = findUsersMatching "*"

    /// Yields users matching the provided name pattern
    let usersMatching name = findUsersMatching name



(*** hide ***)
    // Active Directory Search examples
    //
    //
    //   qbeUser.GivenName <- "*"
    //   s.QueryFilter <- (qbeUser :> Principal)
    //   (searcher.GetUnderlyingSearcher() :?> DirectorySearcher).PageSize <- 1000
    //   (searcher.GetUnderlyingSearcher() :?> DirectorySearcher).SizeLimit <- 0
    //
    //
    //let listUsersThroughGroupSearch() =
    //    use domainContext = new PrincipalContext(ContextType.Domain)
    //    //use user = UserPrincipal.FindByIdentity()
    //    use group = GroupPrincipal.FindByIdentity(domainContext, IdentityType.SamAccountName, "Domain Users")
    //    let mutable i = 0
    //    for u in group.GetMembers(false) do
    //        use user = (u :?> UserPrincipal)
    //        i <- i + 1
    //        printfn "%i %s %s %O %O" i (user.DistinguishedName) user.SamAccountName user.UserPrincipalName user.StructuralObjectClass //(user.AccountExpirationDate.HasValue)
    //
    //
    //let listAllAdGroups() =
    //    use ctx = new PrincipalContext(ContextType.Domain, "ad.hfk.no") //, "OU=HFK");
    //    use group = new GroupPrincipal(ctx)
    //
    //    use search = new PrincipalSearcher(group)
    //    for g in search.FindAll() do
    //        use g = (g :?> GroupPrincipal)
    //        use user = g /// (u :?> UserPrincipal)
    //        printfn "%s || %s" user.Name user.DistinguishedName
    //
    //let directLdapSearch () =
    //    let startingPoint = new DirectoryEntry("LDAP://OU=HFK,DC=ad,DC=hfk,DC=no")
    //    let searcher = new DirectorySearcher(startingPoint)
    //    searcher.Filter = "(&(objectCategory=person)(objectClass=user))" |> ignore
    //    searcher.PageSize <- 1000 //|> ignore
    //    searcher.SizeLimit <- 0 //|> ignore
    //
    //    let mutable i = 0
    //    for res in searcher.FindAll() do
    //        i <- i + 1
    //        printfn "%i %O" i res.Path




(*** define: webservice ***)

module VismaEnterprise =

    type Group = { Id : int }

    type Username = | DomainUser of string | Alias of string

    type User = 
      { VismaId : int
        Email : string
        WorkPhone : string
        MobilePhone : string
        Initials : string       // must be unique
        Type : string           // INTERNAL or EXTERNAL
        GroupMembership : Group list
        DisplayName : string
        UserName : string
        UserNames : Username list }
    with 
        static member Default = 
          { VismaId = -1
            Email = "unknown@example.com"
            WorkPhone = ""
            MobilePhone = ""
            Initials = ""
            Type = "INTERNAL"
            GroupMembership = []
            DisplayName = ""
            UserName = ""
            UserNames = [] }

    let users () =
        // GET - /user
        // read from service
        // translate
        // return:
        [ { User.Default with DisplayName = "One" }
          { User.Default with DisplayName = "Two" }
          { User.Default with DisplayName = "Three" }
          { User.Default with DisplayName = "Four" }
          { User.Default with DisplayName = "Five" }
        ] |> List.toSeq
        




VismaEnterprise.users()

[<Literal>]
let uName = "AARCOMY"
[<Literal>]
let pass = "abc1234"


Http.RequestString
    ( "http://hfk-app01:8090/enterprise_ws/secure/user/5836",
    headers = [ BasicAuth uName pass ] )  


let fullRequest httpMethod uriTail  (formValues : seq<string * string> option) =

    let requestString = sprintf "http://hfk-app01:8090/enterprise_ws/secure/user/%s" uriTail

    match formValues with
    | Some values ->
        Http.RequestString
          ( requestString,
            headers = [ BasicAuth uName pass ],
            body = FormValues values,
            httpMethod = httpMethod )    
    | None ->
        Http.RequestString
          ( requestString,
            headers = [ BasicAuth uName pass ],
            httpMethod = httpMethod )
            
let request uriTail = fullRequest "GET" uriTail None

let userXml vismaId = request vismaId

let usersXml = request ""




[<Literal>]
let fullUser = 
    "<?xml version=\"1.0\" encoding=\"utf-8\" standalone=\"yes\"?>\
     <user email=\"email@hfk.no\" initials=\"12345\" userId=\"1234\" usertype=\"INTERNAL\" \
       xsi:noNamespaceSchemaLocation=\"http://hfk-app01:8090/enterprise_ws/schemas/user-1.1.xsd\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">\
       <groupMembership>\
            <group id=\"1234\"/>\
            <group id=\"4321\"/>\
        </groupMembership>\
        <name displayName=\"Nice Example Name\"/>\
        <usernames username=\"NICE EXAMPLE NAME\">\
            <alias username=\"NICNAME\"/>\
            <alias username=\"12345\"/>\
        </usernames>\
     </user>"

[<Literal>]
let fullUserList = 
    "<users xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:noNamespaceSchemaLocation=\"http://hfk-app01:8090/enterprise_ws/schemas/users-1.0.xsd\">
         <user email=\"email@hfk.no\" initials=\"12345\" userId=\"1234\" usertype=\"INTERNAL\" workPhone=\"s4712345678\" mobilePhone=\"s4712345678\">
            <name displayName=\"Nice Example Name\"/>
            <groupMembership>
                <group id=\"1114\"/>
                <group id=\"1850\"/>
            </groupMembership>
            <usernames username=\"NICE EXAMPLE NAME\">
                <alias username=\"NICNAME\"/>
                <alias username=\"12345\"/>
            </usernames>
        </user>
         <user email=\"email@hfk.no\" initials=\"NICNAME\" userId=\"1234\" usertype=\"INTERNAL\" workPhone=\"s4712345678\" mobilePhone=\"s4712345678\">
            <name displayName=\"Nice Example Name\"/>
            <usernames username=\"NICE EXAMPLE NAME\">
                <alias username=\"NICNAME\"/>
                <alias username=\"12345\"/>
            </usernames>
        </user>
     </users>"

type VeUser = XmlProvider<fullUser>
type VeUsers = XmlProvider<fullUserList>


let tUsers = VeUsers.Parse(usersXml)

let mapToUser (user: VeUsers.User) : VismaEnterprise.User = 
    { VismaId = user.UserId
      Email = user.Email
      WorkPhone = user.WorkPhone
      MobilePhone = user.MobilePhone
      Initials = user.Initials
      Type = user.Usertype
      GroupMembership = []
      DisplayName = user.Name.DisplayName
      UserName = user.Usernames.Username
      UserNames = [] }

for u in tUsers.Users do
    printfn "%O" u.Name.DisplayName




let tUser = VeUser.Parse(userXml "5836")
for n in tUser.Usernames.Alias do
    printf "%s" n.Username.Value



    // Wrap the service call
    // start generating URL strings (with optional params)
    // Generate a whole VE user (XML type provide)
    // Update a single user
    // Update a single user with sub fields n stuff (ie aliases)









(*** define: synch ***)

module Integration = 
    
    type UpdateAction = | Ignore | Add | Update | Deactivate

    [<AutoOpen>]
    module private Imp =

        let (|IsNew|_|) (vu:VismaEnterprise.User) = if vu.VismaId = VismaEnterprise.User.Default.VismaId then Some vu else None
        let (|IsMissing|_|) (adu:ActiveDirectory.User) = if adu.EmployeeId = ActiveDirectory.User.Default.EmployeeId then Some adu else None
        let (|IsExpired|_|) (adu:ActiveDirectory.User) = if not <| adu.IsActive then Some adu else None

        let isChanged (adu:ActiveDirectory.User, vu:VismaEnterprise.User) =
            not (adu.DisplayName = vu.DisplayName       
                 //&& adu.Account = vu. // TODO: check account name changes against the VISMA user changes
                                        // TODO: get "workphone" and other info into the integration
                 && adu.Email = vu.Email )

        let action (adu:ActiveDirectory.User, vu:VismaEnterprise.User) = 
            match adu, vu with
            | IsMissing(adu), vu -> Deactivate
            | IsExpired(adu), vu -> Deactivate
            | adu, IsNew(vu) -> Add
            | user when user |> isChanged -> Update
            | _ -> Ignore

        let matches (adUsers:ActiveDirectory.User seq) (veUsers:VismaEnterprise.User seq) = 

            let accountNameMatches =
                query {
                    for adu in adUsers do
                    join vu in veUsers on (adu.Account = vu.Initials)
                    select (adu, vu) }

            let employeeIdMatches =
                query {
                    for adu in adUsers do
                    join vu in veUsers on (adu.EmployeeId = vu.Initials)
                    select (adu, vu) }

            let matched = accountNameMatches |> union employeeIdMatches

            let unregisteredAdUsers =
                query {
                    for adu in adUsers do
                    where (not <| (matched.Any(fun (ad, vu) -> ad.EmployeeId = adu.EmployeeId)))
                    select (adu, VismaEnterprise.User.Default) }

            let veUsersNotInAd =
                    query { 
                        for vu in veUsers do
                        where (not <| adUsers.Any(fun adu -> adu.Email = vu.Email))
                        select (ActiveDirectory.User.Default, vu) }

            matched |> union unregisteredAdUsers |> union veUsersNotInAd


        let matchActions (users : (ActiveDirectory.User * VismaEnterprise.User) seq) =
            seq { for u in users do yield (u |> action, u) }

    /// Returns all employee actions after comparing the provided sets of users
    let allEmployeeActions ad ve = matches ad ve |> matchActions

    /// Returns necessary actions and employees to synch AD and Visma Enterprise
    let employeeActions ad ve = 
        allEmployeeActions ad ve 
        |> Seq.filter (fun (a, t) -> a <> Ignore)


let testVe = 
        [ { VismaEnterprise.User.Default with DisplayName = "One"; Initials = "123"; VismaId = 123 }
          { VismaEnterprise.User.Default with DisplayName = "Two"; Initials = "1234"; VismaId = 123 }
          { VismaEnterprise.User.Default with DisplayName = "Three"; Initials = "12345"; VismaId = 123 }
          { VismaEnterprise.User.Default with DisplayName = "Four"; Initials = "AABA"; VismaId = 123 }
          { VismaEnterprise.User.Default with DisplayName = "Five"; Initials = "ABBA"; VismaId = 123 }
          { VismaEnterprise.User.Default with DisplayName = "Not in VE"; Initials = "RAWR"; VismaId = 123 }
        ] |> List.toSeq

let testAd = 
        [ { ActiveDirectory.User.Default with DisplayName = "One"; EmployeeId = "123"; Account="" }
          { ActiveDirectory.User.Default with DisplayName = "Two"; EmployeeId = "1234"; Account="" }
          { ActiveDirectory.User.Default with DisplayName = "Three"; EmployeeId = "12345"; Account="" }
          { ActiveDirectory.User.Default with DisplayName = "Four"; EmployeeId = "123456"; Account="AABA" }
          { ActiveDirectory.User.Default with DisplayName = "Five"; EmployeeId = "2121"; Account="ABBA" }
          { ActiveDirectory.User.Default with DisplayName = "Not In AD"; EmployeeId = "999"; Account="OLD" }
        ] |> List.toSeq


Integration.allEmployeeActions testAd testVe
Integration.employeeActions testAd testVe





// Mock webservice
// Get user lists
// Scheduling
// Health reporting
// etc

let adUsers = ActiveDirectory.users() |> Seq.toList |> Seq.take(20)
let veUsers = VismaEnterprise.users() |> Seq.toList

Integration.employeeActions adUsers veUsers
let ff = Integration.allEmployeeActions adUsers veUsers |> Seq.toList



(*** define: list-users ***)

// Get all users
let users = ActiveDirectory.users() |> Seq.toList

// Print all users
for u in users do printfn "%A\r\n" u



let aadwag = ActiveDirectory.usersMatching("Arne*") |> Seq.toList
let testt = ActiveDirectory.usersMatching("Tonje*") |> Seq.toList
let fagskole = ActiveDirectory.usersMatching("Fagsko*") |> Seq.toList

let dis = query { 
            for d in users do
            where (not <| d.IsActive)
            select d } |> Seq.toList
dis
