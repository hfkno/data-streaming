
(**
Active Directoy -> Visma Enterprise Integration
===============================================


Integration overview
====================

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
        Name : string
        Account : string
        IsActive : bool
        Other : string
    }

    /// Dtermines if a user account is active or not at the current moment
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
                yield { Name = user.DisplayName
                        Account = user.SamAccountName
                        IsActive = user |> isActive
                        Other = user.AccountExpirationDate.ToString()
                      }
        }

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






(*

*)




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






// Mock webservice
// Get user lists
// Compare
// Synch diffs
    // Use active patterns to govern the update/change/disablement...
// Scheduling
// Health reporting
// etc



