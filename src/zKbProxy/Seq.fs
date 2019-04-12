namespace ZkbProxy

    module Seq=
        
        let filterMap (xs: seq<bool * 'a>) = 
            xs  |> Seq.filter (fun (x,_)-> x)
                |> Seq.map  (fun (_,x) -> x)
