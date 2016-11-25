
(**
# Active Directoy -> Visma Enterprise Integration

## Integration overview

  * Retreive users from Visma Enterprise
  * Retreive users from Active Directory
  * Synch changes from active directory with Visma user data
  * Push user changes to Visma Enterprise through their user management webservice
*)


(** 
## Integration details
Initial connection to LDAP for user retrieval
*)

(*** include: ldap-connect ***)




(** 
### Implementation

Active Directory is searched using the managed API

*)

(*** include: ad-operations ***)



(*** hide ***)

#r "System.DirectoryServices"
#r "System.DirectoryServices.AccountManagement"
open System.DirectoryServices
open System.DirectoryServices.AccountManagement




(*** define: ad-operations ***)

module ActiveDirectory =

    type User = {
        Name : string
        Account : string
    }

    let users () =
        seq {
            use ctx = new PrincipalContext(ContextType.Domain, "ad.hfk.no", "OU=HFK,DC=ad,DC=hfk,DC=no")
            use qbeUser = new UserPrincipal(ctx)
            use searcher = new PrincipalSearcher(qbeUser)
            for u in searcher.FindAll() do
                let ru = { Name = u.DisplayName; Account = u.SamAccountName }
                yield ru 
        }

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


(*** hide ***)

let users = ActiveDirectory.users() |> Seq.toList
users |> Seq.length
for u in users do printfn "%A\r\n" u




 //use user = UserPrincipal.FindByIdentity(domainContext, IdentityType.Name, "A*")
    
//    use u = new UserPrincipal(domainContext);
//    use s = new PrincipalSearcher(u);
//    s.QueryFilter = (u :> Principal) |> ignore
//
//    for p in s.FindAll() do
//        printfn "%A" p.

//    use user = new UserPrincipal(domainContext)
//    user.GivenName = "Aaron" |> ignore
//
//    use searcher = new PrincipalSearcher(user)
//    searcher.QueryFilter = (user :> Principal) |> ignore
//
//    for res in searcher.FindAll() do 
//        printfn "%O" res





/// This is function that takes three ints
/// Those ints get added together...
let funcWithInfo a b c =
    a + b + c


(*** define: ldap-connect ***)
let helloWorld() = 
    funcWithInfo 1 2 3 |> ignore
    // Visible comments
    // Raw access to source code online
    printfn "Hello world!"



