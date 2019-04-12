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
        let ColName = "zkbkills"
        [<Literal>]
        let UserName = ""
        [<Literal>]
        let UserPassword = ""
        [<Literal>]
        let WebServerPort = 80us
        [<Literal>]
        let LiveBufferSize = 2000
        [<Literal>]
        let DefaultTtl = 10
        let SessionTimeout = TimeSpan.FromMinutes(180.);

    type Configuration=
        {
            KillSourceUri: string;
            NoCache: bool;
            MongoServer: string;
            DbName: string;
            DbCollection: string;
            MongoUserName: string;
            MongoPassword: string;
            WebServerPort: uint16;
            LiveBufferSize: int32;
            SessionTimeout: TimeSpan;
        }
        with             
            static member empty = 
                {   
                    KillSourceUri = ConfigurationDefaults.KillSourceUri;
                    NoCache = false;
                    MongoServer = ConfigurationDefaults.MongoServer; 
                    DbName = ConfigurationDefaults.DbName; 
                    DbCollection = ConfigurationDefaults.ColName; 
                    MongoUserName = ConfigurationDefaults.UserName; 
                    MongoPassword = ConfigurationDefaults.UserPassword;
                    WebServerPort = ConfigurationDefaults.WebServerPort; 
                    LiveBufferSize = ConfigurationDefaults.LiveBufferSize;
                    SessionTimeout = ConfigurationDefaults.SessionTimeout
                }

