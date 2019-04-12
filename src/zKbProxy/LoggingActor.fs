namespace ZkbProxy

    open System

    type LoggingActor()=
    
        let logger = log4net.LogManager.GetLogger(typeof<LoggingActor>)

        let c =  logger.Logger.Repository.Configured

        let logInfo (msg: string) = logger.Info(msg)
            
        let logTrace (msg: string) = logger.Debug(msg)

        let logWarn (msg: string) = logger.Warn(msg)

        let logError (msg: string) = logger.Error(msg)

        let logException (source: string) (ex: System.Exception) = logger.Error(source, ex)

        let pipe = MessageInbox.Start(fun inbox ->
            let rec loop() = async{
            
                let! msg = inbox.Receive()

                match msg with                
                | Warning (source,msg) ->   ("[" + source + "]: " + msg) |> logWarn
                | Error (source, msg) ->    ("[" + source + "]: " + msg) |> logError
                | Exception (source, ex) -> logException source ex               
                | Info (source, msg) ->     ("[" + source + "]: " + msg) |> logInfo
                | Trace (source, msg) ->    ("[" + source + "]: " + msg) |> logTrace
                | _ ->                      ignore 0

                return! loop()
            }

            loop()
        )

        interface IActor with
            member __.Post(msg) = pipe.Post msg
            member __.Request(msg) = pipe.PostAndAsyncReply (fun ch -> msg)
