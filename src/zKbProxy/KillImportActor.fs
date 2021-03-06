﻿namespace ZkbProxy

    open System
    open MongoDB.Driver
    
    type KillImportActor(log: PostMessage, config: Configuration)=
        let msgSource = Actors.messageSource typeof<KillImportActor>.Name        
        let logException (ex: Exception) = ex.Message |> msgSource |> ActorMessage.Error |> log
        let logInfo = msgSource >> ActorMessage.Info >> log
        
        
        let insertOne (col: IMongoCollection<Object>) doc =
            col.InsertOne(doc)

        let write = 
            if not config.NoCache then  
                let dbCollection = 
                    sprintf "Initialising DB connection to %s %s.%s..." config.MongoServer config.DbName config.KillsDbCollection |> logInfo
                    MongoDb.killsCollection config.MongoServer config.DbName config.KillsDbCollection config.MongoUserName config.MongoPassword

                insertOne dbCollection
            else    
                ignore

        let pipe = MessageInbox.Start(fun inbox ->
            let rec loop() = async{
            
                let! msg = inbox.Receive()

                try
                    match msg with                
                    | KillJson json ->                    
                        let data = json |> Bson.ofJson |> (fun b -> match Kills.getKillId b with
                                                                    | Some id -> Some (id,b)
                                                                    | _ -> None)
                        let msg = match data with
                                    | Some (id,bson) -> bson |> write 
                                                        sprintf "KillID %s written to cache" id
                                                            |> fun m -> m |> msgSource |> ActorMessage.Info
                                    | _ -> "Unrecognised message received" |> msgSource |> ActorMessage.Warning
                        
                        msg |> log
                    | Stop ->                   "Stopped kill importer." |>  logInfo
                    | Ping ch ->                ignore 0 |> ch.Reply
                    | _ ->                      ignore 0
                with ex -> logException ex

                return! loop()
            }

            loop()
        )

        do pipe.Error.Add(logException)

        interface IActor with
            member __.Post(msg) = pipe.Post msg
            member __.Request(msg) = pipe.PostAndAsyncReply (fun ch -> msg)
            member __.Ping() = pipe.PostAndAsyncReply (fun ch -> Ping ch)