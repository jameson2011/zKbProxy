namespace ZkbProxy

open System
open ZkbProxy
open Suave
open ZkbProxy.CommandLine
open ZkbProxy.Strings
open System.Threading

module Program=
    
    open Newtonsoft.Json.Linq

    let jsonToConfig(j: JObject) =
        let toJValue (token: JToken) = token :?> JValue
        
        let toBool = Option.ofNull >> Option.map (toJValue >> (fun value -> value.ToObject<bool>()))
        let toInt32 = Option.ofNull >> Option.map (toJValue >> (fun value -> value.ToObject<int32>()))
        let toUint16 = Option.ofNull >> Option.map (toJValue >> (fun value -> value.ToObject<uint16>()))
        let toString = Option.ofNull >> Option.map (toJValue >> (fun value -> value.ToObject<string>()))
        let toTimeSpan = Option.ofNull >> Option.map (toJValue >> (fun value -> value.ToObject<TimeSpan>()))
        let defaultConfig = Configuration.empty

        { Configuration.KillSourceUri = j.["killSourceUri"] |> toString |> Option.defaultValue defaultConfig.KillSourceUri;
            ZkbApiBaseUri =             j.["zkbApiBaseUri"] |> toString |> Option.defaultValue defaultConfig.ZkbApiBaseUri;
            UserAgent =                 j.["userAgent"] |> toString |> Option.defaultValue defaultConfig.UserAgent;
            NoCache =                   j.["noCache"] |> toBool |> Option.defaultValue defaultConfig.NoCache;
            MongoServer =               j.["mongoServer"] |> toString |> Option.defaultValue defaultConfig.MongoServer;
            DbName =                    j.["dbName"] |> toString |> Option.defaultValue defaultConfig.DbName;
            KillsDbCollection =         j.["killsDbCollection"] |> toString |> Option.defaultValue defaultConfig.KillsDbCollection;
            SessionsDbCollection =      j.["sessionsDbCollection"] |> toString |> Option.defaultValue defaultConfig.SessionsDbCollection;
            MongoUserName =             j.["mongoUserName"] |> toString |> Option.defaultValue defaultConfig.MongoUserName;
            MongoPassword =             j.["mongoPassword"] |> toString |> Option.defaultValue defaultConfig.MongoPassword;
            WebServerPort =             j.["webServerPort"] |> toUint16 |> Option.defaultValue defaultConfig.WebServerPort;
            BufferSize =                j.["bufferSize"] |> toInt32 |> Option.defaultValue defaultConfig.BufferSize;
            SessionTimeout =            j.["sessionTimeout"] |> toTimeSpan |> Option.defaultValue defaultConfig.SessionTimeout;
        }



    let private configFromFile(filePath: string) =
        
        if not <| System.IO.File.Exists(filePath) then
            let msg = filePath |> sprintf "The configuration file %s was not found."
            raise (System.IO.FileNotFoundException(msg))
        
        filePath 
            |> System.IO.File.ReadAllText 
            |> JObject.Parse
            |> jsonToConfig

    let private configFromCmdLine(app: CommandLine.App)=        
        {   Configuration.empty with
                 KillSourceUri = CommandLine.getKillSourceUriValue app 
                                        |> Option.defaultValue ConfigurationDefaults.KillSourceUri
                 MongoServer = CommandLine.getMongoServerValue app 
                                        |> Option.defaultValue ConfigurationDefaults.MongoServer;
                 DbName = CommandLine.getMongoDbValue app 
                                        |> Option.defaultValue ConfigurationDefaults.DbName;
                 KillsDbCollection = CommandLine.getMongoKillsCollectionValue app 
                                        |> Option.defaultValue ConfigurationDefaults.KillsColName;
                 SessionsDbCollection = CommandLine.getMongoSessionsCollectionValue app
                                        |> Option.defaultValue ConfigurationDefaults.SessionsColName;
                 MongoUserName = CommandLine.getMongoUserValue app 
                                        |> Option.defaultValue ConfigurationDefaults.UserName;
                 MongoPassword = CommandLine.getMongoPasswordValue app 
                                        |> Option.defaultValue ConfigurationDefaults.UserPassword;
                 WebServerPort = CommandLine.getWebServerPortValue app 
                                        |> Option.defaultValue 80us;
                 BufferSize = CommandLine.getLiveBufferSizeValue app 
                                        |> Option.defaultValue ConfigurationDefaults.BufferSize;
                 NoCache = CommandLine.getNoCacheValue app;
                 SessionTimeout = CommandLine.getSessionTimeoutArg app;
        }   

    let private configFromStartApp(app: CommandLine.App)=        
        app 
            |> CommandLine.getConfigFileValue
            |> Option.map configFromFile 
            |> Option.defaultWith (fun () -> configFromCmdLine app)
        
    let private validateConfig (config: Configuration) = 
        let validateString value name  =            
            match value with
            | NullOrWhitespace _ -> invalidArg name "Missing argument"
            | _ -> ignore 0

        let validateValue value name min max = 
            match value with
            | x when x < min || x > max -> invalidArg name "Argument out of range."
            | _ -> ignore 0

        validateString config.KillSourceUri CommandLine.killSourceUriArg
        validateString config.MongoServer CommandLine.dbServerArg
        validateString config.DbName CommandLine.dbNameArg
        validateString config.KillsDbCollection CommandLine.dbKillsCollectionArg
        validateString config.SessionsDbCollection CommandLine.dbSessionCollectionArg

        validateValue config.BufferSize CommandLine.bufferSizeArg 0 1000000
        validateValue config.WebServerPort CommandLine.webPortArg 80us 65535us
        
        config


    let private startProxy (app: CommandLine.App) =        
        let config = configFromStartApp app |> validateConfig
                
        let cts = new CancellationTokenSource()
        let ctsWeb = new CancellationTokenSource()
        let services = ServiceFactory(config)
        let logger = services.Logger
        let logInfo msg = ActorMessage.Info ("zKbProxy", msg) |> logger.Post
        let source = services.Source :> IActor
        
        let statsProvider = services.StatisticsProvider 
        let importer = services.Importer


        "Starting zKB capture..." |> logInfo
        (GetNextKillFromSource (config.KillSourceUri, TimeSpan.Zero)) |> source.Post

        "Starting web app..." |> logInfo
        let webConfig = WebApp.webConfig (WebLogger logger.Post) config.WebServerPort
        let webRoutes = WebApp.webRoutes logger.Post services.SessionProvider statsProvider services.ZkbApi

        let listening,server = startWebServerAsync webConfig webRoutes 

        Async.Start(server, ctsWeb.Token)
        
        let shutdown() =
            async {
                ctsWeb.Cancel()
                (Stop) |> source.Post
                (Stop) |> importer.Post
                let! _ = source.Ping()
                let! _ = importer.Ping()
                let! _ = logger.Ping()
                
                return true
            }

        Console.WriteLine("Proxy started. Hit CTRL-C to quit")
        Console.CancelKeyPress.Add(fun x -> 
            "Shutting down services..." |> logInfo
            
            let _ = shutdown() |> Async.RunSynchronously
            Console.Out.WriteLine("Services shutdown")
            
            cts.Cancel()
            
            )
        
        WaitHandle.WaitAll( [| cts.Token.WaitHandle |] ) |> ignore
        
        
        Console.Out.WriteLine("Done")
        true

    let private createAppTemplate()=
        let app = CommandLine.createApp()
                    |> addRun startProxy
                    |> setHelp
    
        app.OnExecute(fun () -> app.ShowHelp()
                                0)
        app


    [<EntryPoint>]
    let main argv = 
        let app = createAppTemplate()
    
        app.OnExecute(fun () -> app.ShowHelp()
                                0)
        try
                app.Execute(argv)
        with    
        | :? System.ApplicationException as ex ->   
                ConsoleUtils.error ex.Message
                2
        | ex -> ConsoleUtils.error ex.Message   
                2

