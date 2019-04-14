namespace ZkbProxy

    type ServiceFactory(config : Configuration)=

        let reportConfig (log: ActorMessage -> unit) (config: Configuration) = 
            [
                "Starting with config:";
                sprintf "Web port:            %d" config.WebServerPort;
                sprintf "Mongo server:        %s" config.MongoServer;
                sprintf "Mongo DB:            %s" config.DbName;
                sprintf "Kills collection:    %s" config.KillsDbCollection;
                sprintf "Sessions collection: %s" config.SessionsDbCollection;
                sprintf "Kill source:         %s" config.KillSourceUri;
                sprintf "No Caching:          %b" config.NoCache;
                sprintf "buffer size:         %i" config.BufferSize;
                sprintf "Session timeout:     %f" config.SessionTimeout.TotalMinutes;
            ] 
            |> Strings.join System.Environment.NewLine
            |> (fun s -> ActorMessage.Info (typeof<ServiceFactory>.Name, s))
            |> log
            config


        let logActor = LoggingActor() :> IActor
        let postLog = logActor.Post
        do reportConfig postLog config |> ignore


        let statsActor = StatisticsActor(postLog) 
        let postStats = (statsActor :> IActor).Post

        let createKillProvider name =
            if config.NoCache then  
                NoDbKillProviderActor(postLog, postStats, config, name) :> IKillProviderActor
            else    
                KillProviderActor(postLog, postStats, config, name) :> IKillProviderActor

        let killProvider =  createKillProvider Actors.mainProviderName
        let sessionProvider = SessionProviderActor(postLog, postStats, config, killProvider, createKillProvider)

        let sourceForward = match config.NoCache with
                            | true ->   Actors.broadcast [  (killProvider :?> IActor).Post; 
                                                            (sessionProvider :> IActor).Post; 
                                                            postStats ]
                            | _ ->      let dumpActor = KillImportActor(postLog, config) :> IActor
                                        Actors.broadcast [  (killProvider :?> IActor).Post; 
                                                            (sessionProvider :> IActor).Post; 
                                                            dumpActor.Post; postStats ]

        let killSource = ZkbSourceActor(postLog, sourceForward)

        let initializeStats() =
            async {
                let! killCount = (killProvider).GetKillCount() 
                ActorMessage.SetKillCount killCount |> postStats
                return true
            }

        
        member __.Source = killSource

        member __.KillProvider = killProvider

        member __.SessionProvider = sessionProvider

        member __.Logger = logActor

        member __.StatisticsProvider = 
            initializeStats() |> Async.RunSynchronously |> ignore
            statsActor 

        