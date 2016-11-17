
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
open System.DirectoryServices.AccountManagement


let theAction() =
    use context = new PrincipalContext(ContextType.Domain, "ad.hfk.no")
    use searcher = new PrincipalSearcher(new UserPrincipal(context))

    for res in searcher.FindAll() do
        printfn "%O" res

    ()

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



