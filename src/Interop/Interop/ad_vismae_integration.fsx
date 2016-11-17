
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







let directActiono() =
    use ctx = new PrincipalContext(ContextType.Domain, "ad.hfk.no") //, "OU=HFK");
    use group = new GroupPrincipal(ctx)

    use search = new PrincipalSearcher(group)
    for g in search.FindAll() do
        use g = (g :?> GroupPrincipal)
        use user = g /// (u :?> UserPrincipal)
        printfn "%s || %s" user.Name user.UserPrincipalName

directActiono()




// This one is getting the right info at least - but is using the old API and gets kinda ugly at the element level
let directAction() =
    use searcher = new DirectorySearcher()
    searcher.Filter = "(&(objectClass=user))" |> ignore
    for sr in searcher.FindAll() do
        printfn "%O" sr.Properties.["manager"]

directAction()




// We can list users... need to filter for "users" and not just "eeeeeverything" - this is through ther
let theAction() =
    use domainContext = new PrincipalContext(ContextType.Domain) //, "ad.hfk.no")
    //use user = UserPrincipal.FindByIdentity()
    use group = GroupPrincipal.FindByIdentity(domainContext, IdentityType.SamAccountName, "Domain Users")
    for u in group.GetMembers(false) do
        use user = (u :?> UserPrincipal)
        printfn "%s %s %O" (user.DistinguishedName) user.SamAccountName user.UserPrincipalName //(user.AccountExpirationDate.HasValue)

theAction()



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



