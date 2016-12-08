
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

Visma Enterprise is located as "http://hfk-app01:8080/enterprise/enterprise?0"

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
open System.Net
open System.Collections.Generic
open FSharp.Data
open FSharp.Data.HttpRequestHeaders

// TODO: run through the UserNames usage in the update routine and the change comparison routine - ensure that everybody has their employee ID and their user name as an alias, if not then add!

// TODO: dry run solution on a limited selection of users and ensure we're kosher

// TODO: prep for full run (tomorrow)



// TODO: Find out from VISMA if there is a way to update the display name... It seems not.  Currently disabled in the "Update" functionality.  "DisplayName" as well as "UserName" are inaccesible

// TODO: Setup a time report of hours used on the integration

// TODO: create internal documentation in CMDB with the details

// TODO: create a credentials solution... Passwords should not be stored in project files...

// TODO: document where we are "loading" data and where we are "writing" data in the documentation (ie `toUser` in this file)




(*** define: utility ***)

let union a b = (a:IEnumerable<'a>).Union(b)
let isEmpty s = String.IsNullOrWhiteSpace(s)
let safeString s = match s |> isNull with | true -> "" | _ -> s

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
        WorkPhone : string
        MobilePhone : string
        FirstName : string
        LastName : string }
    with 
        static member Default = 
          { EmployeeId = "-1"
            DisplayName = "Unknown User"
            Account = ""
            Email = "unknown@example.com"
            IsActive = false
            WorkPhone = ""
            MobilePhone = ""
            FirstName = "Unknown"
            LastName = "User" }

    let private printProperties (user : UserPrincipal) = 
        let de = user.GetUnderlyingObject() :?> DirectoryEntry
        for name in de.Properties.PropertyNames do
            let pname = name :?> string
            printfn "[%s]: %s" pname (de.Properties.[pname].Value.ToString())

    let toDirectoryEntry (user : UserPrincipal) = user.GetUnderlyingObject() :?> DirectoryEntry

    let getDirectoryProperty prop (directoryEntry : DirectoryEntry) = 
        let p = directoryEntry.Properties.[prop]
        match p.Value with
        | v when v |> isNull -> ""
        | _ -> p.Value.ToString()

    let getProp prop = toDirectoryEntry >> getDirectoryProperty prop

    /// Determines if a user account is active or not at the current moment
    let private isActive (user : UserPrincipal)  = 
        not <| (user.AccountExpirationDate.HasValue && user.AccountExpirationDate.Value < DateTime.Now)

    /// EmployeeIds are stored as the carLicense property with a birthdate suffixed
    let private employeeId (user: UserPrincipal) =
        try
            let license = user |> getProp "carLicense"
            match license with
            | v when v |> isEmpty -> User.Default.EmployeeId
            | license ->
                let birthDateLength = 6
                license.Substring(0, license.Length - birthDateLength)
        with
        | _ -> 
            failwith (sprintf "Error reading employeeId for user '%s' (%s)" user.DisplayName user.SamAccountName)

    let private mobilePhone user = user |> getProp "mobile"

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
                        DisplayName = user.DisplayName |> safeString
                        Email = user.EmailAddress |> safeString
                        Account = user.SamAccountName
                        IsActive = user |> isActive
                        WorkPhone = user.VoiceTelephoneNumber |> safeString
                        MobilePhone = user |> mobilePhone
                        FirstName = user.GivenName |> safeString
                        LastName = user.Surname |> safeString } }

    /// Yields all users - quite slow on occasion...
    ///     Performance info:
    //          Real: 00:00:36.172, CPU: 00:00:08.640, GC gen0: 15, gen1: 7, gen2: 0
    //          Real: 00:00:29.275, CPU: 00:00:07.796, GC gen0: 15, gen1: 2, gen2: 0
    //          Real: 00:00:30.239, CPU: 00:00:09.000, GC gen0: 15, gen1: 8, gen2: 0
    let users () = findUsersMatching "*"

    /// Yields users matching the provided name pattern
    let usersMatching name = findUsersMatching name

    
(*** hide ***)

module VismaEnterpriseAnticorruption = 

    type ActiveDirectory.User with
        member x.FormattedAccount = x.Account.ToUpper()
        member x.NecessaryAliases() = [ x.FormattedAccount; x.EmployeeId ]
        member x.UserName = (sprintf "%s %s" x.FirstName x.LastName).ToUpper()


(*** define: webservice ***)

/// Visma Enterprise operations and management
module VismaEnterprise =

    open VismaEnterpriseAnticorruption

    type Group = { Id : int }

    type Username = 
        | DomainUser of name : string | Alias of name : string
    with
        static member name uname =
            match uname with | DomainUser v | Alias v -> v

    /// Visma Enterprise User information
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
        member x.HasAlias alias =
            x.UserNames.Any(fun uname -> Username.name uname = alias)
        member x.HasAllAliasesFor (user: ActiveDirectory.User) =
            user.NecessaryAliases() |> List.forall x.HasAlias

    module UserService =
        
        [<AutoOpen>]
        module Endpoint =

            [<Literal>]
            let fullUser = 
                "<?xml version=\"1.0\" encoding=\"utf-8\" standalone=\"yes\"?>\
                 <user email=\"email@hfk.no\" initials=\"s12345\" userId=\"1234\" usertype=\"INTERNAL\" \
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
                     <user email=\"email@hfk.no\" initials=\"s12345\" userId=\"1234\" usertype=\"INTERNAL\" workPhone=\"s4712345678\" mobilePhone=\"s4712345678\">
                        <name displayName=\"Nice Example Name\"/>
                        <groupMembership>
                            <group id=\"1114\"/>
                            <group id=\"1850\"/>
                        </groupMembership>
                        <usernames username=\"NICE EXAMPLE NAME\">
                            <alias username=\"NICNAME\"/>
                            <alias username=\"s12345\"/>
                        </usernames>
                    </user>
                     <user email=\"email@hfk.no\" initials=\"NICNAME\" userId=\"1234\" usertype=\"INTERNAL\" workPhone=\"s4712345678\" mobilePhone=\"s4712345678\">
                        <name displayName=\"Nice Example Name\"/>
                        <groupMembership>
                            <group id=\"1114\"/>
                            <group id=\"1850\"/>
                        </groupMembership>
                        <usernames username=\"NICE EXAMPLE NAME\">
                            <alias username=\"NICNAME\"/>
                            <alias username=\"s12345\"/>
                        </usernames>
                    </user>
                 </users>"

            [<Literal>]
            let uName = "AARCOMY"
            [<Literal>]
            let pass = "abc1234"

            let fullRequest httpMethod uriTail  (formValues : (string * string) list option) =
                let requestString = sprintf "http://hfk-app01:8090/enterprise_ws/secure/user/%s" uriTail
                match formValues with
                | Some values ->
                    Http.RequestString
                      ( requestString,
                        httpMethod = httpMethod,
                        headers = [ BasicAuth uName pass ],
                        body = FormValues values )
                | None ->
                    Http.RequestString
                      ( requestString,
                        httpMethod = httpMethod,
                        headers = [ BasicAuth uName pass ] )

            let request uriTail = fullRequest "GET" uriTail None
            let simpleRequest httpMethod uriTail = fullRequest httpMethod uriTail None
            let put = simpleRequest "PUT"

            type VeServiceUser  = XmlProvider<fullUser>
            type VeServiceUsers = XmlProvider<fullUserList>

            let toUser (user: VeServiceUsers.User) : User = 
                { VismaId = user.UserId
                  Email = user.Email
                  WorkPhone = user.WorkPhone
                  MobilePhone = user.MobilePhone
                  Initials = user.Initials
                  Type = user.Usertype
                  GroupMembership = [ for g in user.GroupMemberships do yield { Group.Id = g.Id } ]
                  DisplayName = user.Name.DisplayName
                  UserName = user.Usernames.Username
                  UserNames = [ for a in user.Usernames.Alias do yield Username.Alias a.Username ] }

        let userXml vismaId = request vismaId
        let usersXml = request ""
        let users () = 
            (usersXml |> VeServiceUsers.Parse)
                .Users 
                |> Seq.map toUser

        let safeAction action errorMessage =
            try
                action ()
            with
            // The webservice returns "bad request" (code 400) to indicate an internal server error
            | :? WebException as webEx -> failwith errorMessage

        let putContent action message = safeAction (fun () -> action |> put) message

        let setEmailType emailType userId email =
            putContent
                (sprintf "%i/email/%s/%s" userId emailType email)
                (sprintf "Email address '%s' in use, could not update user" email)

        let setEmail userId email = setEmailType "WORK" userId email
            
        let setPhone phoneType userId number = 
            sprintf "%i/phone/%s/%s" userId phoneType number |> put
        let setMobile = setPhone "MOBILE"
        let setWorkPhone = setPhone "WORK"

        let setInitials userId initials =
            putContent
                (sprintf "%i/initials/%s" userId initials)
                (sprintf "Initials cannot be changed, could not update user to initials '%s'" initials)

        let addAlias userId alias =
            fullRequest "POST" (sprintf "%i/username" userId) (Some [ "user", alias ]) |> ignore

//        // Did not delete aliases as expected... :
//        let deleteAlias userId alias =
//            simpleRequest "DELETE" (sprintf "%s/username/alais/%s" userId alias) |> ignore

        let createUser employeeId userName firstName lastName initials email =
            let urlTail = sprintf "new/firstname/%s/lastname/%s/initials/%s/workemail/%s" firstName lastName initials email
            let formValues = [ "alias", (sprintf "%s;%s" employeeId userName ) ]
            safeAction 
                (fun () -> fullRequest "POST" urlTail (Some formValues)) 
                (sprintf "Could not create user '%s'" initials)

        /// Deletes the users alias, OR sets the user passive if the alias is the same as the users initials 
        let deleteUser userId initialsOrAlias =
            simpleRequest "DELETE" (sprintf "%i/username/%s" userId initialsOrAlias)

        /// Deletes the users alias, OR sets the user passive if the alias is the same as the users initials 
        let deactivateUser userId initialsOrAlias = deleteUser userId initialsOrAlias

    let users () = UserService.users ()



(*** define: synch ***)

module Integration = 
    
    open VismaEnterpriseAnticorruption

    type UpdateAction = | Ignore | Add | Update | Deactivate

    [<AutoOpen>]
    module Actions =

        let (|IsUnregistered|_|) (vu:VismaEnterprise.User) = if vu.VismaId = VismaEnterprise.User.Default.VismaId then Some vu else None
        let (|IsMissing|_|) (adu:ActiveDirectory.User) = if adu.EmployeeId = ActiveDirectory.User.Default.EmployeeId then Some adu else None
        let (|IsInactive|_|) (adu:ActiveDirectory.User) = if not <| adu.IsActive then Some adu else None

        let needsUpdating (adu:ActiveDirectory.User, vu:VismaEnterprise.User) =
            not (adu.DisplayName = vu.DisplayName 
                 && adu.WorkPhone = vu.WorkPhone
                 && adu.MobilePhone = vu.MobilePhone
                 && adu.EmployeeId = vu.Initials
                 && adu.Email = vu.Email
                 && vu.HasAllAliasesFor(adu) )

        let action (adu:ActiveDirectory.User, vu:VismaEnterprise.User) = 
            match adu, vu with
            | IsMissing (adu), vu -> Deactivate
            | IsInactive(adu), vu -> Deactivate
            | adu, IsUnregistered(vu) -> Add
            | user when user |> needsUpdating -> Update
            | _ -> Ignore

        let matches (adUsers:ActiveDirectory.User seq) (veUsers:VismaEnterprise.User seq) = 

            let accountNameMatches =
                query { for adu in adUsers do
                        join vu in veUsers on (adu.FormattedAccount = vu.Initials)
                        select (adu, vu) }

            let employeeIdMatches =
                query { for adu in adUsers do
                        join vu in veUsers on (adu.EmployeeId = vu.Initials)
                        select (adu, vu) }

            let matched = accountNameMatches |> union employeeIdMatches

            let unregisteredAdUsers =
                query { for adu in adUsers do
                        where (not <| (matched.Any(fun (ad, vu) -> ad.EmployeeId = adu.EmployeeId))) 
                        select (adu, VismaEnterprise.User.Default) }

            let veUsersNotInAd =
                    query { for vu in veUsers do
                            where (not <| adUsers.Any(fun adu -> adu.EmployeeId = vu.Initials || adu.FormattedAccount = vu.Initials))
                            select (ActiveDirectory.User.Default, vu) }

            matched |> union unregisteredAdUsers |> union veUsersNotInAd


        let matchActions (users : (ActiveDirectory.User * VismaEnterprise.User) seq) =
            seq { for u in users do yield (u |> action, u) }

        let processk = 0
            

    /// Returns all employee actions after comparing the provided sets of users
    let employeeActionsVerbose ad ve = matches ad ve |> matchActions

    /// Returns necessary actions and employees to synch AD and Visma Enterprise
    let employeeActions ad ve = 
        employeeActionsVerbose ad ve 
        |> Seq.filter (fun (a, t) -> a <> Ignore)


    let processEmployeeAction ((action, (au, vu)) : UpdateAction * (ActiveDirectory.User * VismaEnterprise.User)) = 
        printfn "Processing %A command for %s (%s::%s)" action au.DisplayName vu.DisplayName vu.Initials
        match action with
        | Ignore -> ()
        | Add -> VismaEnterprise.UserService.createUser au.EmployeeId au.FormattedAccount au.FirstName au.LastName au.FormattedAccount au.Email |> ignore
        | Update -> 
            if au.Email <> vu.Email             then VismaEnterprise.UserService.setEmail vu.VismaId au.Email |> ignore
            if au.WorkPhone <> vu.WorkPhone     then VismaEnterprise.UserService.setWorkPhone vu.VismaId au.WorkPhone |> ignore
            if au.MobilePhone <> vu.MobilePhone then VismaEnterprise.UserService.setMobile vu.VismaId au.MobilePhone |> ignore
            //if au.UserName <> vu.UserName then VismaEnterprise.WebService.s    ...   // the webservice currently has no username editing support
            //if au.DisplayName <> vu.DisplayName then VismaEnterprise.WebService....  // the webservice currently has no display name editing support
            if not <| vu.HasAllAliasesFor(au) then
                for alias in au.NecessaryAliases() do
                    if not <| vu.HasAlias(alias) then
                        VismaEnterprise.UserService.addAlias vu.VismaId alias
        | Deactivate -> VismaEnterprise.UserService.deactivateUser vu.VismaId vu.Initials |> ignore

    let processEmployeeActions actions = actions |> Seq.map processEmployeeAction



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


Integration.employeeActionsVerbose testAd testVe |> Seq.toList
Integration.employeeActions testAd testVe

#time
let rawr = 123
#time


#time
let adUsers = ActiveDirectory.users() |> Seq.where(fun u -> u.DisplayName.StartsWith("Ag")) |> Seq.toList
#time
#time
let veUsers = VismaEnterprise.users() |> Seq.where(fun u -> u.DisplayName.StartsWith("Ag")) |> Seq.toList
#time
veUsers |> List.length
adUsers |> List.length


let aus = query { for a in adUsers do
                  //where (a.DisplayName.Contains("Andre"))
                  sortBy a.EmployeeId
                  select a } 
                  //|> Seq.take(150) 
                  |> Seq.toList


let ves = query { for a in veUsers do
                  //where (a.DisplayName.Contains("Andre"))
                  sortBy a.Initials
                  select a } 
                  //|> Seq.take(150) 
                  |> Seq.toList



Integration.employeeActionsVerbose aus ves |> Seq.map (fun (a, (b,c)) -> a, b.EmployeeId, b.DisplayName, c.Initials, c.DisplayName) |> Seq.toList
Integration.employeeActions aus ves |> Seq.map (fun (a, (b,c)) -> a, b.EmployeeId, b.DisplayName, b.Account.ToUpper(), c.Initials, c.DisplayName) |> Seq.toList 
let arne = Integration.employeeActions aus ves |> Seq.filter (fun (a, (b,c)) -> c.Initials = "ARNNESS") |> Seq.toList |> Seq.head |> snd
query { for adu in aus do
        join vu in ves on (adu.Account.ToUpper() = vu.Initials)
        select (adu, vu) } 
    |> Seq.map (Integration.Actions.action)
    |> Seq.toList




let updateTest = Integration.employeeActions adUsers veUsers |> Seq.skip 0 |> Seq.head
Integration.processEmployeeAction updateTest



let updateActions = Integration.employeeActions adUsers veUsers |> Seq.toList

Integration.processEmployeeActions updateActions //|> ignore


let a, (a1, a2) = updateActions |> List.head

let needsUpdating (adu:ActiveDirectory.User, vu:VismaEnterprise.User) =
    not (adu.DisplayName = vu.DisplayName 
            && adu.WorkPhone = vu.WorkPhone
            && vu.HasAllAliasesFor(adu)
            && adu.EmployeeId = vu.Initials
            && adu.Email = vu.Email )

needsUpdating (a1,a2)
a1.DisplayName
a2.DisplayName
a1.WorkPhone    // my workphone is coming up null... should check for null and that it is being read correctly...
a2.WorkPhone
a2.HasAllAliasesFor(a1)
a1.EmployeeId
a2.Initials
a1.Email
a2.Email

Integration.Actions.needsUpdating (a1, a2)



let ff = Integration.employeeActionsVerbose adUsers veUsers |> Seq.toList


(*** define: list-users ***)

// Get and print all users
let users = ActiveDirectory.users() |> Seq.toList
for u in users do printfn "%A\r\n" u


let aadwag = ActiveDirectory.usersMatching("Arne*") |> Seq.toList
let testt = ActiveDirectory.usersMatching("Tonje*") |> Seq.toList
let fagskole = ActiveDirectory.usersMatching("Fagsko*") |> Seq.toList

let dis = query { 
            for d in users do
            where (not <| d.IsActive)
            select d } |> Seq.toList
dis

