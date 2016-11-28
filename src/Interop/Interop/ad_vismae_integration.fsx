
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
open System
open System.DirectoryServices
open System.DirectoryServices.AccountManagement
open System.Linq


(*** define: ad-operations ***)

/// Active Directory operations and management
module ActiveDirectory =

    /// AD User account information
    type User = {
        EmployeeId : string
        DisplayName : string
        Account : string
        Email : string
        IsActive : bool
        Other : string
    }

    /// Determines if a user account is active or not at the current moment
    let private isActive (user : UserPrincipal)  = 
        not <| (user.AccountExpirationDate.HasValue && user.AccountExpirationDate.Value < DateTime.Now)

    /// Yields domain users that match the provided pattern
    let private findUsersMatching (pattern) =
        seq {
            use context = new PrincipalContext(ContextType.Domain, "ad.hfk.no", "OU=HFK,DC=ad,DC=hfk,DC=no")
            use userSearch = new UserPrincipal(context)
            userSearch.GivenName <- pattern
            use search = new PrincipalSearcher(userSearch)

            for principal in search.FindAll() do
                let user = (principal :?> UserPrincipal)
                yield { EmployeeId = user.EmployeeId
                        DisplayName = user.DisplayName
                        Email = user.EmailAddress
                        Account = user.SamAccountName
                        IsActive = user |> isActive
                        Other = user.SamAccountName } }

    /// Yields all users
    let users () = findUsersMatching "*"

    /// Yields users matching the provided name pattern
    let usersMatching name = findUsersMatching name


(*** hide ***)
    // Search examples
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

    type Group = {
        Id : int
    }

    type Username = | DomainUser of string | Alias of string

    type User = {
        VismaId : int
        Email : string
        WorkPhone : string
        MobilePhone : string
        Initials : string       // must be unique
        Type : string           // INTERNAL or EXTERNAL
        GroupMembership : Group list
        DisplayName : string
        UserName : string
        UserNames : Username list
    }

    let DefaultUser = {
        VismaId = -1
        Email = "unknown@example.com"
        WorkPhone = ""
        MobilePhone = ""
        Initials = ""
        Type = "INTERNAL"
        GroupMembership = []
        DisplayName = ""
        UserName = ""
        UserNames = []
    }

    let users () =
        // GET - /user
        // read from service
        // translate
        // return:
        [ { DefaultUser with DisplayName = "One" }
          { DefaultUser with DisplayName = "Two" }
          { DefaultUser with DisplayName = "Three" }
          { DefaultUser with DisplayName = "Four" }
          { DefaultUser with DisplayName = "Five" }
        ] |> List.toSeq
        




(*** define: synch ***)

type UpdateAction = | Ignore | Update | Deactivate  // TODO: need to check the syntax for deactivating users, this might also be an "update" - check if add needs its own operation

let synchDemo (ad: (string * int) list) (ve: (string * int) list) =
    // pattern match in query
    //  for adU in ad do
    //  select (Comparison(adU, ve.FirstOrDefault(email)))  // null match = add, other match = ignore or update -- return (UpdateAction, User)


    // selet veInfo that`s not inside the adInfo and use it for @delete@ commands
    ()



let ad = [("one@one.com",1);("two@one.com",2);("three@one.com",5);("four@one.com",6;("five@one.com",9);]
let ve = [("one@one.com",1);("two@one.com",19);("three@one.com",123);]

synchDemo ad ve

let synch (adUsers:ActiveDirectory.User list) (veUsers:VismaEnterprise.User list) =
//
//    let matches = 
//        query {
//            for adu in adUsers do
//            select adu
//        }
//
//    let 


    // left outer join adUsers, mi


    // adUsers not in other list: add
    // adUsers with same info: drop
    // adUsers with new info: push
    // veUsers not in the liist anymore need to be deactivated...
    ()


let adUsers = ActiveDirectory.users() |> Seq.toList
let veUsers = VismaEnterprise.users() |> Seq.toList






synch adUsers veUsers







// Mock webservice
// Get user lists
// Compare
// Synch diffs
    // Use active patterns to govern the update/change/disablement...
// Scheduling
// Health reporting
// etc




(*** define: list-users ***)

// Get all users
let users = ActiveDirectory.users() |> Seq.toList

// Print all users
for u in users do printfn "%A\r\n" u


let dis = query { 
            for d in users do
            where (not <| d.IsActive)
            select d } |> Seq.toList
dis

