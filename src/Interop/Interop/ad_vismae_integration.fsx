
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

(*** hide ***)

#r "System.DirectoryServices"
#r "System.DirectoryServices.AccountManagement"
open System.DirectoryServices
open System.DirectoryServices.AccountManagement


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






//module ActiveDirectory =



// We can list users... need to filter for "users" and not just "eeeeeverything" - this is through the new API and works kinda nicely
let theActio() =
    use domainContext = new PrincipalContext(ContextType.Domain) //, "ad.hfk.no")

    use group = GroupPrincipal.FindByIdentity(domainContext, IdentityType.SamAccountName, "Domain Users")
    let mutable i = 0
    for u in group.GetMembers(false) do
        use user = (u :?> UserPrincipal)
        i <- i + 1
        printfn "%i %s %s %O %O" i (user.DistinguishedName) user.SamAccountName user.UserPrincipalName user.StructuralObjectClass //(user.AccountExpirationDate.HasValue)

theActio()



// Direct search, getting an interesting number of results (3000)
let listOrganizationalUnits () =
    let startingPoint = new DirectoryEntry("LDAP://OU=HFK,DC=ad,DC=hfk,DC=no")
    let searcher = new DirectorySearcher(startingPoint)
    searcher.Filter = "(&(objectCategory=person)(objectClass=user))" |> ignore
    searcher.PageSize <- 500 //|> ignore
    searcher.SizeLimit <- 0 //|> ignore

    let mutable i = 0
    for res in searcher.FindAll() do
        i <- i + 1
        printfn "%i %O" i res.Path
    ()
listOrganizationalUnits()







// We can list users... need to filter for "users" and not just "eeeeeverything" - this is through the new API and works kinda nicely
let theAction() =
    use domainContext = new PrincipalContext(ContextType.Domain) //, "ad.hfk.no")
    //use user = UserPrincipal.FindByIdentity()
    use group = GroupPrincipal.FindByIdentity(domainContext, IdentityType.SamAccountName, "Domain Users")
    let mutable i = 0
    for u in group.GetMembers(false) do
        use user = (u :?> UserPrincipal)
        i <- i + 1
        printfn "%i %s %s %O %O" i (user.DistinguishedName) user.SamAccountName user.UserPrincipalName user.StructuralObjectClass //(user.AccountExpirationDate.HasValue)

theAction()







// This one seems to only be getting the goups, not he members...  Perhaps a user filter on the PrincipalSearcher?
let directActiono() =
    use ctx = new PrincipalContext(ContextType.Domain, "ad.hfk.no") //, "OU=HFK");
    use group = new GroupPrincipal(ctx)

    use search = new PrincipalSearcher(group)
    for g in search.FindAll() do
        use g = (g :?> GroupPrincipal)
        use user = g /// (u :?> UserPrincipal)
        printfn "%s || %s" user.Name user.DistinguishedName

directActiono()




// This one is getting the right info at least - but is using the old API and gets kinda ugly at the element level
let directAction() =
    use searcher = new DirectorySearcher()
    searcher.Filter = "(&(objectClass=user))" |> ignore
    let mutable i = 0
    for sr in searcher.FindAll() do
        i <- i + 1
        printfn "%i %O" i sr.Properties.["manager"]

directAction()





// Syntax for the findbyident search - not working
let directActionoo() =
    use ctx = new PrincipalContext(ContextType.Domain, "ad.hfk.no") //, "OU=HFK");
    use user = UserPrincipal.FindByIdentity(ctx, "cn=Aaron Winston Comyn")
    printfn "%s || %s" user.Name user.UserPrincipalName

directActionoo()






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



