namespace ZkbProxy

    open System

    module ConfigurationDefaults = 
        [<Literal>]
        let KillSourceUri = "https://redisq.zkillboard.com/listen.php?ttw=10"
        [<Literal>]
        let MongoServer = "127.0.0.1"
        [<Literal>]
        let DbName = "zkbproxy"
        [<Literal>]
        let KillsColName = "zkbkills"
        [<Literal>]
        let SessionsColName = "sessions"
        [<Literal>]
        let UserName = ""
        [<Literal>]
        let UserPassword = ""
        [<Literal>]
        let WebServerPort = 80us
        [<Literal>]
        let BufferSize = 2000
        [<Literal>]
        let DefaultTtl = 10
        let SessionTimeout = TimeSpan.FromMinutes(180.);

    type Configuration=
        {
            KillSourceUri: string;
            NoCache: bool;
            MongoServer: string;
            DbName: string;
            KillsDbCollection: string;
            SessionsDbCollection: string;
            MongoUserName: string;
            MongoPassword: string;
            WebServerPort: uint16;
            BufferSize: int32;
            SessionTimeout: TimeSpan;
        }
        with             
            static member empty = 
                {   
                    KillSourceUri = ConfigurationDefaults.KillSourceUri;
                    NoCache = false;
                    MongoServer = ConfigurationDefaults.MongoServer; 
                    DbName = ConfigurationDefaults.DbName; 
                    KillsDbCollection = ConfigurationDefaults.KillsColName; 
                    SessionsDbCollection = ConfigurationDefaults.SessionsColName;
                    MongoUserName = ConfigurationDefaults.UserName; 
                    MongoPassword = ConfigurationDefaults.UserPassword;
                    WebServerPort = ConfigurationDefaults.WebServerPort; 
                    BufferSize = ConfigurationDefaults.BufferSize;
                    SessionTimeout = ConfigurationDefaults.SessionTimeout
                }

