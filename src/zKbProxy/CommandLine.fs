namespace ZkbProxy

module CommandLine=        
        
    open System
    open Microsoft.Extensions
    
    type App = CommandLineUtils.CommandLineApplication
    type CmdOption = CommandLineUtils.CommandOption
    type Command = App -> bool

    let killSourceUriArg = "kill"
    let dbServerArg = "svr"
    let dbNameArg = "db"
    let dbKillsCollectionArg = "killscol"
    let dbSessionCollectionArg = "sessionscol"
    let dbUserArg = "un"
    let dbPasswordArg = "pw"
    let webPortArg = "port"
    let bufferSizeArg = "buffer"
    let noCacheArg = "nocache"
    let sessionTimeoutArg = "sessiontimeout"
    let configArg = "config"

    let private longestArg = 
        [ killSourceUriArg; dbServerArg; dbNameArg; dbKillsCollectionArg; dbSessionCollectionArg;
            dbUserArg; dbPasswordArg; webPortArg; bufferSizeArg; noCacheArg; 
            sessionTimeoutArg; configArg; ]
        |> Seq.map String.length
        |> Seq.max

    let setDesc desc (app: App) =
        app.Description <- desc
        app

    let addSingleOption (name: string) fullName desc (app:App)=
        let pad = String(' ', max 0 (longestArg - name.Length) )
        let tag = sprintf "-%s%s | --%s" name pad fullName
        app.Option(tag, desc,CommandLineUtils.CommandOptionType.SingleValue) |> ignore
        app
    
    let addSwitchOption (name: string) fullName desc (app:App)=
        let pad = String(' ', max 0 (longestArg - name.Length) )
        let tag = sprintf "-%s%s | --%s" name pad fullName
        app.Option(tag, desc,CommandLineUtils.CommandOptionType.NoValue) |> ignore
        app

    
    let setAction (fn: Command) (app: App) =
        let act = fun () ->     if fn(app) then 0
                                else 2
        app.OnExecute(act) 
        app
    
    let getOption shortName (app: App)  =
        app.Options
        |> Seq.tryFind (fun o -> o.ShortName = shortName)
    
    let getStringOption shortName (app:App) =
        match (getOption shortName app) with
        | Some x when x.HasValue() -> x.Value() |> Some
        | _ -> None 



    let getMultiStringOption shortName app = 
        let v = getOption shortName app
        match v with
        | Some vs -> List.ofSeq vs.Values
        | _ -> []

    let getSwitchOption (shortName: string) (app:App)=
        match (getOption shortName app) with
        | Some x -> x.Values.Count > 0
        | _ -> false


    let setHelp(app: App) =
        app.HelpOption("-? | -h | --help") |> ignore
        app

    let createApp() =
        let app = new App(false)
        app.Name <- "ZkbProxy"
        app.Description <- "A proxy for zKb's RedisQ service"
        app
    
    let addConfigFileArg =              addSingleOption configArg configArg "The configuration file"
    let getConfigFileValue app =        getStringOption configArg app

    let addKillSourceUriArg =           addSingleOption killSourceUriArg "killsource" "The URI providing zKB kills. By default this is zKB RedisQ"
    let getKillSourceUriValue app =     getStringOption killSourceUriArg app

    let addSessionTimeoutArg =          addSingleOption sessionTimeoutArg sessionTimeoutArg (sprintf "Timeout for adhoc sessions. Default: %d minutes" (ConfigurationDefaults.SessionTimeout.TotalMinutes |> int ))
    let getSessionTimeoutArg app = 
        match getStringOption sessionTimeoutArg app with
        | None  -> ConfigurationDefaults.SessionTimeout
        | Some x -> 
            match UInt32.TryParse(x) with
            | (true,x) -> TimeSpan.FromMinutes(float x)
            | _ -> failwith "Invalid session timeout. Must be a positive non-zero integer."

    let addNoCacheArg =                     addSwitchOption noCacheArg noCacheArg "Do not write killmails to MongoDB. If ommitted, killmails are written to cache. Previously cached kills are available."
    let getNoCacheValue app =               getSwitchOption noCacheArg app
    
    let addMongoServerArg =                 addSingleOption dbServerArg "server" ("The MongoDB server name. Default: " + ConfigurationDefaults.MongoServer)
    let getMongoServerValue app =           getStringOption dbServerArg app
    
    let addMongoDbArg =                     addSingleOption dbNameArg dbNameArg ("The MongoDB DB name. Default: " + ConfigurationDefaults.DbName)
    let getMongoDbValue app =               getStringOption dbNameArg  app

    let addMongoKillsCollectionArg =        addSingleOption dbKillsCollectionArg "killscol" ("The kills DB collection name. Default: " + ConfigurationDefaults.KillsColName)
    let getMongoKillsCollectionValue app =  getStringOption dbKillsCollectionArg app    

    let addMongoSessionsCollectionArg =        addSingleOption dbSessionCollectionArg "sessionscol" ("The sessions DB collection name. Default: " + ConfigurationDefaults.SessionsColName)
    let getMongoSessionsCollectionValue app =  getStringOption dbSessionCollectionArg app    

    let addMongoUserArg =                   addSingleOption dbUserArg "user" "User name for MongoDB. Default: no auth is assumed."
    let getMongoUserValue app =             getStringOption dbUserArg app    
    let addMongoPasswordArg =               addSingleOption dbPasswordArg "password" "MongoDB user password"
    let getMongoPasswordValue app =         getStringOption dbPasswordArg app
    
    let addWebServerPortArg =               addSingleOption webPortArg "port" ("The proxy's web server port. Default: " + ConfigurationDefaults.WebServerPort.ToString())
    let getWebServerPortValue app =     
        match getStringOption webPortArg app with
        | None  -> None
        | Some x -> 
            match UInt16.TryParse(x) with
            | (true,x) -> Some x
            | _ -> None             
    
    let addLiveBufferSizeArg =          addSingleOption bufferSizeArg bufferSizeArg ("The maximum kill count of stream buffers. Default: " + ConfigurationDefaults.BufferSize.ToString())
    let getLiveBufferSizeValue app =    
        match getStringOption bufferSizeArg app with
        | None -> None
        | Some x -> 
            match Int32.TryParse(x) with
            | (true,x) -> Some x
            | _ -> None

    
    
    let private composeAppPipe(f: App -> App) = new System.Action<App>(f >> setHelp >> ignore)
    
    // verbs:
    let addRun cmd (app: App) =
        let f = setDesc "Run the proxy" 
                                    >> addKillSourceUriArg
                                    >> addMongoServerArg >> addMongoDbArg >> addMongoKillsCollectionArg >> addMongoSessionsCollectionArg
                                    >> addMongoUserArg >> addMongoPasswordArg 
                                    >> addWebServerPortArg
                                    >> addLiveBufferSizeArg
                                    >> addNoCacheArg
                                    >> addSessionTimeoutArg
                                    >> addConfigFileArg
                                    >> setAction cmd
        app.Command("run", (composeAppPipe f)) |> ignore
        app
