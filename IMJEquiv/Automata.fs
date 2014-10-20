﻿namespace IMJEquiv
open IMJEquiv
open System

type State =
  inherit IComparable

[<CustomComparison>]
[<StructuralEquality>]
type IntState =
  {
    Val : Int
  }

  interface State with
    override x.CompareTo (yobj: Object) : Int =
      match yobj with
      | :? IntState as y -> compare x y  
      | _ -> 1

[<CustomComparison>]
[<StructuralEquality>]
type PairState =
  {
    State : State
    Store : Store
  }
  
  interface State with
    member x.CompareTo (yobj: Object) : Int =
      match yobj with
      | :? PairState as y -> compare x y  
      | _ -> -1

type Label = Move * Store

// This needs looked at
type StackConst = State

type TransLabel =
  | Push of Set<RegId> * Label * StackConst * Set<RegId>
  | Pop of  Set<RegId> * Label * StackConst * Set<RegId>
  | Noop of Set<RegId> * Label

type Transition =
  | SetT of State * Set<RegId> * State
  | LabelT of State * TransLabel * State
  | PermT of State * Map<RegId, RegId> * State

type Player =
  | O
  | P

type Automaton =
 {
   States : List<State>
   Owner : Map<State,Player>
   InitS : State
   TransRel : List<Transition>
   InitR : List<RegId>
   Final : List<State>
 }

module Automata = 

  let maxint = 5

  let labelOfTrans (t: Transition) : Option<Label> =
    match t with
    | LabelT (_, tl, _) ->
        match tl with
        | Push (_, l, _, _)
        | Pop  (_, l, _, _)
        | Noop (_, l)       -> Some l
    | _                 -> None

//  let storesOfAutomaton (a: Automaton) : Set<Store> =
//    let folder (ss: Set<Store>)  


//  let renStore (s: Store) (ren: Map<RegId, RegId>) : Store =
//    let tryapplyR (r: RegId) : RegId =
//      if Map.containsKey r ren then ren.[r] else r
//    let tryapply (x : Val) : Val =
//      match x with
//      | Num _
//      | Star
//      | Nul -> x
//      | Reg r -> Reg (tryapplyR r)
//    let folder acc r (i, m) =
//      let innerfolder acc f v = Map.add f (tryapply v) acc
//      Map.add (tryapplyR r) (i, Map.fold innerfolder Map.empty m) acc 
//    Map.fold folder Map.empty s
    
 

  let muSupp (ms: List<Move>) : Set<RegId> =
    List.fold (fun acc m -> Set.union acc (Move.supp m)) Set.empty ms

  let labelSupp ((m,s): Label) : Set<RegId> =
    Set.union (Move.supp m) (Store.supp s)

  let private stateCount = ref 0

  let newState () : State =
    do stateCount := !stateCount + 1
    { Val = !stateCount } :> State

  let twoStateAuto (l: Label) : Automaton =
    let q0 = newState ()
    let qF = newState ()
    let owner = Map.ofList [(q0, P); (qF, O)]
    let trans =
      LabelT (q0, Noop (Set.empty, l), qF)
    {
      States = [q0; qF]
      Owner = owner
      InitS = q0
      TransRel = [trans]
      InitR = Set.toList (labelSupp l)
      Final = [qF]
    }


  let nu (d: ITbl) (a: Automaton) (r0: Store) : Automaton =
    let q0' = { State = a.InitS; Store = r0 }
    
    let rec fix (ts: Set<Transition>, rs: Set<Store>, frontier: Set<Store>) : Set<Transition> * Set<Store> * Set<Store> =
      if Set.isEmpty frontier 
      then 
        (ts, rs, frontier) 
      else
        let folder (ts', rs', fs') (r: Store) =
          let mkTransFromTrans (acc: Set<Transition>, newrs: Set<Store>) (t: Transition) : Set<Transition> * Set<Store> = 
            match t with
            | SetT (qo, x, qo') when a.Owner.[qo] = O ->
                if Set.isEmpty (Set.intersect x (Map.domain r)) 
                then
                  let qor = { State = qo; Store = r } :> State 
                  let qo'r = { State = qo'; Store = r } :> State
                  let acc' = Set.add (SetT (qor, x, qo'r)) acc
                  (acc', newrs)
                else 
                  (acc, newrs)
            | SetT (qp, x, qp') when a.Owner.[qp] = P ->
                let r' = Map.restrict r x
                let x' = Set.difference x (Map.domain r)
                let qpr = { State = qp; Store = r } :> State
                let qp'r' = { State = qp'; Store = r' } :> State
                let acc' = Set.add (SetT (qpr, x', qp'r')) acc
                let newrs' = if Set.contains r' rs then newrs else Set.add r' newrs
                (acc', newrs')
            | PermT (qo, pi, qo') ->
                let r' = Store.postPermute pi (Perm.preApply r pi)
                let qor = { State = qo; Store = r } :> State
                let qo'r = { State = qo'; Store = r' } :> State
                let t = PermT (qor, pi, qo'r)
                let acc' = Set.add t acc
                let newrs' = if Set.contains r' rs then newrs else Set.add r' newrs 
                (acc', newrs')
            | LabelT (qo, tl, qp) when a.Owner.[qo] = O ->
                match tl with
                | Push (x, (mu, s), _, _) 
                | Pop  (x, (mu, s), _, _) 
                | Noop (x, (mu, s))       ->
                    let domR = Map.domain r
                    let suppMu = Move.supp mu
                    let b1 = Map.isSubset r s
                    let b2 = Set.isEmpty (Set.intersect x domR)
                    let b3 =
                      let cond1 ri = not (Set.contains ri suppMu)
                      let cond2 ri = not (Set.contains ri (Map.domain (Map.difference s r)))
                      Set.forall (fun ri -> cond1 ri && cond2 ri) domR
                    let acc' =
                      if b1 && b2 && b3 then
                        let qor = { State = qo; Store = r } :> State
                        let qpr = { State = qp; Store = r } :> State
                        let acc' = 
                          let tl' =
                            match tl with
                            | Push (_, _, q, y) ->
                                let newq = { State = q; Store = r } :> State 
                                Push (x, (mu, s), newq, y)
                            | Pop  (_, _, q, y) -> 
                                let newq = { State = q; Store = r } :> State
                                Pop  (x, (mu, s), newq, y)
                            | Noop  _ -> tl
                          Set.add (LabelT (qor, tl', qpr)) acc
                        acc'
                      else 
                        acc
                    (acc', newrs)
            | LabelT (qp, tl, qo) when a.Owner.[qp] = P ->
                match tl with
                | Push (x, (mu, s), _, _) 
                | Pop  (x, (mu, s), _, _) 
                | Noop (x, (mu, s))       ->
                    let qpr = { State = qp; Store = r } :> State
                    let suppMu = Move.supp mu
                    let domS = Map.domain s
                    let domR = Map.domain r
                    let zStore = Store.trim s (Set.union suppMu (Set.difference domS (Set.union x domR)))
                    let z = Store.supp zStore
                    let s' = Map.restrict s z
                    let r' = Map.difference s s'
                    let x' = Set.intersect (Set.union x domR) z
                    let qor' = { State = qo; Store = r' } :> State
                    let tl' = 
                      match tl with
                      | Push (_, _, q, y) ->
                          let newq = { State = q; Store = r' } :> State 
                          Push (x', (mu, s'), newq, y)
                      | Pop  (_, _, q, y) -> 
                          let newq = { State = q; Store = r' } :> State
                          Pop  (x', (mu, s'), newq, y)
                      | Noop  _ -> Noop (x', (mu, s')) 
                    let acc' = Set.add (LabelT (qpr, tl', qor')) acc
                    let newrs' = if Set.contains r' rs then newrs else Set.add r' newrs
                    (acc', newrs')
          let newTrans, newFront = List.fold mkTransFromTrans (ts', fs') a.TransRel
          let newRs = Set.union rs' newFront
          (newTrans, newRs, newFront) 
        let newts, newrs, newfs = Set.fold folder (ts, rs, Set.empty) frontier 
        fix (newts, newrs, newfs)   
      
    let newts, newrs, _ = fix (Set.empty, Set.singleton r0, Set.singleton r0)
    let stPairs = Set.product (set a.States) newrs
    let owner = Set.fold (fun m (q,r) -> Map.add ({ State = q; Store = r } :> State) (a.Owner.[q]) m) Map.empty stPairs
    let finals = Set.fold (fun xs (q,r) -> { State = q; Store = r } :> State :: xs) [] stPairs
    {
      States = Map.domainList owner
      Owner = owner
      InitS = q0'
      TransRel = Set.toList newts
      InitR = Set.toList (Set.difference (set a.InitR) (Map.domain r0))
      Final = finals 
    }
      


  let rec fromCanon (d: ITbl) (g: TyEnv) (cn: Canon) (mu: List<Move>) (s: Store) : Automaton =
    match cn with
    | NullR -> twoStateAuto (ValM Nul, s)
    | Var x -> 
        let k = Types.getPosInTyEnv x g
        twoStateAuto (mu.[k], s)
    | If (x, c1, c0) ->
        let k = Types.getPosInTyEnv x g
        if mu.[k] = ValM (Num 0) then
          fromCanon d g c0 mu s 
        else
          fromCanon d g c1 mu s
    | Let (_, Assn (x,f,y), c) ->
        let (ValM (Reg rk')) = mu.[Types.getPosInTyEnv x g]
        let (ValM (Reg rj')) = mu.[Types.getPosInTyEnv y g]
        let newStore = Store.update s rk' f (Reg rj')
        let trimmedStore = Store.trim newStore (muSupp mu)
        let cAuto = fromCanon d g c mu trimmedStore
        let q0 = newState ()
        let owner = Map.updateOrDefault q0 (fun _ -> P) P cAuto.Owner
        let trans = 
          SetT (q0, Map.domain trimmedStore, cAuto.InitS)
        {
          States = q0 :: cAuto.States
          Owner = owner
          InitS = q0
          TransRel = trans :: cAuto.TransRel
          InitR = Map.domainList s
          Final = cAuto.Final
        }
     | Let (x, NullL ty, c) ->
        let q0 = newState ()
        let mu' = List.append mu  [ValM Nul]
        let g' = List.append g [(x, ty)]
        let cAuto = fromCanon d g' c mu' s
        let owner = Map.updateOrDefault q0 (fun _ -> P) P cAuto.Owner
        let trans = 
          SetT (q0, set cAuto.InitR, cAuto.InitS)
        {
          States = q0 :: cAuto.States
          Owner = owner
          InitS = q0
          TransRel = trans :: cAuto.TransRel
          InitR = cAuto.InitR
          Final = cAuto.Final
        }
     | Let (x, CanLet.Num i, c) ->
        let q0 = newState ()
        let mu' = List.append mu  [ValM (Num i)]
        let g' = List.append g [(x, Int)]
        let cAuto = fromCanon d g' c mu' s
        let owner = Map.updateOrDefault q0 (fun _ -> P) P cAuto.Owner
        let trans = 
          SetT (q0, set cAuto.InitR, cAuto.InitS)
        {
          States = q0 :: cAuto.States
          Owner = owner
          InitS = q0
          TransRel = trans :: cAuto.TransRel
          InitR = cAuto.InitR
          Final = cAuto.Final
        }
     | Let (x, Skip, c) ->
        let q0 = newState ()
        let mu' = List.append mu  [ValM Star]
        let g' = List.append g [(x, Ty.Void)]
        let cAuto = fromCanon d g' c mu' s
        let owner = Map.updateOrDefault q0 (fun _ -> P) P cAuto.Owner 
        let trans = 
          SetT (q0, set cAuto.InitR, cAuto.InitS)
        {
          States = q0 :: cAuto.States
          Owner = owner
          InitS = q0
          TransRel = trans :: cAuto.TransRel
          InitR = cAuto.InitR
          Final = cAuto.Final
        } 
     | Let (x, Plus (y,z), c) ->
        let q0 = newState ()
        let (ValM (Num yval)) = mu.[Types.getPosInTyEnv y g]
        let (ValM (Num zval)) = mu.[Types.getPosInTyEnv z g]
        let mu' = List.append mu  [ValM (Num (yval + zval))]
        let g' = List.append g [(x, Int)]
        let cAuto = fromCanon d g' c mu' s
        let owner = Map.updateOrDefault q0 (fun _ -> P) P cAuto.Owner
        let trans = 
          SetT (q0, set cAuto.InitR, cAuto.InitS)
        {
          States = q0 :: cAuto.States
          Owner = owner
          InitS = q0
          TransRel = trans :: cAuto.TransRel
          InitR = cAuto.InitR
          Final = cAuto.Final
        }
     | Let (y, Eq (x1, x2), c) -> 
        let q0 = newState ()
        let (ValM (Reg x1val)) = mu.[Types.getPosInTyEnv x1 g]
        let (ValM (Reg x2val)) = mu.[Types.getPosInTyEnv x2 g]
        let cmp = if x1val = x2val then 1 else 0
        let mu' = List.append mu  [ValM (Num cmp)]
        let g' = List.append g [(y, Int)]
        let cAuto = fromCanon d g' c mu' s
        let owner = Map.updateOrDefault q0 (fun _ -> P) P cAuto.Owner
        let trans = 
          SetT (q0, set cAuto.InitR, cAuto.InitS)
        {
          States = q0 :: cAuto.States
          Owner = owner
          InitS = q0
          TransRel = trans :: cAuto.TransRel
          InitR = cAuto.InitR
          Final = cAuto.Final
        }
     | Let (y, Cast (i, x), c) ->
         let (ValM (Reg rk')) = mu.[Types.getPosInTyEnv x g]
         let j, _ = s.[rk']
         if Types.subtype d j i then 
           let mu' = List.append mu [ValM (Reg rk')]
           fromCanon d g c mu' s
         else
           let q0 = newState ()
           let owner = Map.singleton q0 P 
           {
             States = [q0]
             Owner = owner
             InitS = q0
             TransRel = []
             InitR = Map.domainList s
             Final = []
           }
     | Let (y, Fld (x,f), c) -> 
        let q0 = newState ()
        let (ValM (Reg rk')) = mu.[Types.getPosInTyEnv x g]
        let (i,m) = s.[rk']
        let v  = m.[f]
        let ty = Types.ofFld d i f 
        let mu' = List.append mu  [ValM v]
        let g' = List.append g [(y, ty)]
        let cAuto = fromCanon d g' c mu' s
        let owner = Map.updateOrDefault q0 (fun _ -> P) P cAuto.Owner
        let trans = 
          SetT (q0, set cAuto.InitR, cAuto.InitS)
        {
          States = q0 :: cAuto.States
          Owner = owner
          InitS = q0
          TransRel = trans :: cAuto.TransRel
          InitR = cAuto.InitR
          Final = cAuto.Final
        }
     | Let (x, CanLet.Call (y,m,zs), c) ->
        let (Iface yi) = Types.getTyfromTyEnv y g
        let (_, xty) = Types.ofMeth d yi m
        let (ValM (Reg rj)) = mu.[Types.getPosInTyEnv y g]
        let mapper z : Val =
          match mu.[Types.getPosInTyEnv z g] with
            | ValM v -> v
            | _ -> failwith "Expected a value move."
        let vs = List.map mapper zs
        let q0 = newState ()
        let q1 = newState ()
        let callm = Call (rj, m, vs)
        let l = Noop (Set.empty, (callm, s))
        let calltr = LabelT (q0, l, q1)

        let states0 = [q0; q1]
        let owner0 = Map.singleton q0 P
        let trel0 = [calltr]
        let final0 = []
        let initS0 = q0
        let initR0 = Set.toList (Map.domain s)
        
        let getPair st =
          let oldrs = Map.domain s
          let newrs = Map.domain st
          let nuX = Set.difference newrs oldrs
          (nuX, newrs)

        match xty with
          | Void ->
              let z0 = muSupp mu
              let allStores = Store.stores d Map.empty s z0
              let folder (states, owner: Map<State,Player>, trel, final) s0' =
                let (nuX, rY) = getPair s0'
                let mu' = List.append mu [ValM Star]
                let g'  = List.append g [(x, xty)]
                let autoc = fromCanon d g' c mu' s0'
                let q' = newState ()
                let ret1 = LabelT (q1, Noop (nuX, (Ret (rj,m,Star), s0')), q')
                let ret2 = SetT (q', rY, autoc.InitS)
                let states' = q' :: states @ autoc.States
                let owner' =
                  Map.map (fun (k: State) v -> if k = q' then P elif List.contains k states then owner.[k] else v) autoc.Owner
                let trel' = [ret1; ret2] @ trel @ autoc.TransRel
                let final' = final @ autoc.Final
                (states', owner', trel', final')
              let (states, owner, trel, final) = List.fold folder (states0, owner0, trel0, final0) allStores 
              {
                States = states
                Owner = owner
                InitS = initS0
                TransRel = trel
                InitR = initR0
                Final = final
              }
          | Iface i ->
              let z0 = muSupp mu
              let domS = Map.domain s
              let rj's = (Store.nextReg domS) :: Set.toList domS
              let rj'folder (states', owner': Map<State,Player>, trel', final') rj' =
                let z0' = Set.add rj' z0 
                let allStores = Store.stores d Map.empty s z0'
                let mu' = List.append mu [ValM (Reg rj')]
                let g'  = List.append g [(x, xty)]
                let s0'folder (states, owner, trel, final) s0' =
                  let (nuX, rY) = getPair s0'
                  let autoc = fromCanon d g' c mu' s0'
                  let q' = newState ()
                  let ret1 = LabelT (q1, Noop (nuX, (Ret (rj,m,Reg rj'), s0')), q')
                  let ret2 = SetT (q', rY, autoc.InitS)
                  let states'' = q' :: states @ autoc.States
                  let owner'' = Map.map (fun (k: State) v -> if k = q' then P elif List.contains k states then owner'.[k] else v) autoc.Owner
                  let trel'' = [ret1; ret2] @ trel @ autoc.TransRel
                  let final'' = final @ autoc.Final
                  (states'', owner'', trel'', final'')
                let (states, owner, trel, final) = List.fold s0'folder (states', owner', trel', final') allStores 
                (states, owner, trel, final)
              let (states, owner, trel, final) = List.fold rj'folder (states0, owner0, trel0, final0) rj's
              {
                States = states
                Owner = owner
                InitS = initS0
                TransRel = trel
                InitR = initR0
                Final = final
              }  
          | Int ->
              let z0 = muSupp mu
              let domS = Map.domain s
              let allStores = Store.stores d Map.empty s z0
              let s0'folder (states, owner, trel, final) s0' =
                let (nuX, rY) = getPair s0'
                let jfolder (states', owner': Map<State,Player>, trel', final') j =
                  let mu' = List.append mu [ValM (Num j)]
                  let g'  = List.append g [(x, xty)]
                  let autoc = fromCanon d g' c mu' s0'
                  let q' = newState ()
                  let ret1 = LabelT (q1, Noop (nuX, (Ret (rj,m,Num j), s0')), q')
                  let ret2 = SetT (q', rY, autoc.InitS)
                  let states'' = q' :: states @ autoc.States
                  let owner'' =
                    Map.map (fun (k: State) v -> if k = q' then P elif List.contains k states then owner'.[k] else v) autoc.Owner
                  let trel'' = [ret1; ret2] @ trel @ autoc.TransRel
                  let final'' = final @ autoc.Final
                  (states'', owner'', trel'', final'')
                let js = [0..maxint]
                let (states', owner', trel', final') = List.fold jfolder (states, owner, trel, final) js
                (states', owner', trel', final')
              let (states, owner, trel, final) = List.fold s0'folder (states0, owner0, trel0, final0) allStores
              {
                States = states
                Owner = owner
                InitS = initS0
                TransRel = trel
                InitR = initR0
                Final = final
              }
//     | Let (_, While (r, c1), c2) -> 
//         let (ValM (Reg rk')) = mu.[Types.getPosInTyEnv r g]
//         if (snd s.[rk']).["val"] = Num 0 then
//           fromCanon d g c2 mu s
//         else
           

//  and fromCanLet (d: TyEnv) (g: ITbl) (c: CanLet) (l: Label) : Automaton =
//    match c with
//    | Skip -> 
