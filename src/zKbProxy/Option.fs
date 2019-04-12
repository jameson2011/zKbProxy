namespace ZkbProxy

    module Option=   
        let ofNull<'a when 'a : null> (v: 'a) =
            if System.Object.ReferenceEquals(v, null) then
                None
            else
                Some v

        let defaultValue (defaultValue: 'a) (value: 'a option)=
            match value with
            | Some x -> x
            | _ -> defaultValue
            
