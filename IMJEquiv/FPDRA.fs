﻿//
// This file defines a kind of FPDRA, but it is still a special kind of automaton
// which is specific to the IMJ equivalence problem.  These FPDRA are like ordinary 
// FPDRA(S#) except in a few ways:
//
//   * They inherit the two kinds of transitions from IMJ2A, namely the normal 
//     transitions which operate on stacks of pairs and the divergence transitions 
//     that only operate on normal stacks. However, apart from the fact that they 
//     may operate over stacks of pairs, pushing into separate components, the 
//     transitions are otherwise quite standard.
//
//   * The atomic operations associated with transitions operate over sets of 
//     letters at a time, rather than individual letters.
//
//   * For the purpose of translating from IMJ2A, we fix the set of states as objects 
//     which, in particular, have an associated span, which is a pair of mappings 
//     that say how the registers of the two automata in the IMJ2A correspond to 
//     the registers in this FPDRA.  However, this information is only relevant 
//     during the translation and it plays no part in the semantics of the FPDRA, 
//     which is standard excepting the points above.
//
// These three perculiarities of this special kind of FPDRA are "compiled away" by the
// translation to standard FPDRS(S#) in FPDRS.fromFPDRA.
//

namespace IMJEquiv

/// A span records the way that, in a given state, the registers in the 
/// first automaton of the IMJ2A correspond to the registers in the
/// FPDRA (Span.Left) and the registers in the second automaton of the IMJ2A
/// correspond to the registers in the FPDRA (Span.Right).  For example, if 
/// the first automaton's register 2 is represented by FPDRA register 3, then
/// this can be indicated by a span s with s.Left.[2] = 3.
type Span = 
  {
    Left: Map<RegId,RegId>
    Right: Map<RegId, RegId>
  }
  
  override x.ToString () =
    let ls = Map.toList x.Left
    let rs = Map.toList x.Right
    let lstr = List.toStringWithDelims "{ " ", " " }" ls
    let rstr = List.toStringWithDelims "{ " ", " " }" rs
    sprintf "L = %s R = %s" lstr rstr

  static member range (s: Span) : Set<Int> = 
    Map.codomain s.Left + Map.codomain s.Right

/// The FPDRA has states that contain the underlying IMJ2A state,
/// the current span and 
type SpanState = 
  {
    State: IMJ2AState
    Span: Span
    Store: Store
    Owner: Player
  }

  override x.ToString () =
    sprintf "%O[%O][%O]" x.State x.Store x.Span

type PushData = 
  StackConst * Set<RegId>

type PopData =
  StackConst * Set<RegId>

type TLabel =
  | Cut of Set<RegId>
  | Noop of Set<RegId>
  | Push of Set<RegId> * PushData * PushData
  | Push1 of Set<RegId> * PushData
  | Pop of Set<RegId> * PopData * PopData
  | Pop1 of Set<RegId> * PushData
  | PopFre of StackConst
  | Eps 

  override x.ToString() =
    let showSet xs = 
      List.toStringWithDelims "{ " ", " " }" (Set.toList xs)
    match x with
    | Cut xs -> "Cut " + showSet xs
    | Noop xs -> "nu " + showSet xs
    | Push (xs, (p1,ys1), (p2,ys2)) -> sprintf "nu %s. Push (%O,%s) (%O,%s)" (showSet xs) p1 (showSet ys1) p2 (showSet ys2)
    | Push1 (xs, (p1,ys1)) -> sprintf "nu %s. Push (%O,%s)" (showSet xs) p1 (showSet ys1)
    | Pop  (xs, (p1,ys1), (p2,ys2)) -> sprintf "nu %s. Pop (%O,%s) (%O,%s)" (showSet xs) p1 (showSet ys1) p2 (showSet ys2) 
    | Pop1 (xs, (p1,ys1)) -> sprintf "nu %s. Pop (%O,%s)" (showSet xs) p1 (showSet ys1)
    | PopFre p -> sprintf "Pop*(%O)" p
    | Eps -> "eps"

type Trans = SpanState * TLabel * SpanState

type FPDRA =
  {
    States: List<SpanState>
    Finals: List<SpanState>
    Initial: SpanState
    Transitions: List<Trans>
    NumRegs: Int
  }

type Freshness = Player

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module FPDRA =

  let newHalfSpans (m: Map<RegId,RegId>) (xs: List<RegId>) (usd: Set<Int>, fr: Set<Int>)  : Seq<Map<RegId,RegId>> =
    let rec nsp m xs (ud, fr) =
      match xs with
      | []    -> Seq.singleton m
      | y::ys -> 
          let f = Set.minElement fr
          seq { 
            for b in [true;false] do  // choose whether to add something fresh or not
              if b then 
                for m' in nsp m ys (ud, Set.remove f fr) do
                  yield Map.add y f m' 
              else
                for i in ud do
                  for m' in nsp m ys (Set.remove i ud, fr) do
                    yield Map.add y i m' 
          }
    nsp m xs (usd, fr)
    
  let newSpans (n: Int) (fr: Freshness) (xs1: Set<RegId>) (xs2: Set<RegId>) (sp: Span) : Seq<Span> =
    let xs1' = Set.toList xs1
    let xs2' = Set.toList xs2
    let ranL = Map.codomain sp.Left
    let ls = Set.difference (set {1..n}) ranL
    let ranR = Map.codomain sp.Right
    let rs = Set.difference (set {1..n}) ranR
    let avail = Set.intersect ls rs
    let newLs, newRs = 
      match fr with
      | O -> 
          let lspans = newHalfSpans sp.Left xs1' (ls - avail,avail)
          let rspans = seq {
              for m in lspans do
                let ls' = Map.codomain m
                let avail' = avail - ls'
                yield! newHalfSpans sp.Right xs2' (rs - avail',avail')
            }
          (lspans,rspans)
      | P -> 
          let lspans = newHalfSpans sp.Left xs1' (Set.empty, avail) // Globally fresh implies no used names allowed
          let rspans = seq {
              for m in lspans do
                let ranL' = Map.codomain m
                let fr  = ranL' - ranL
                let avail' = avail - fr
                yield! newHalfSpans sp.Right xs2' (fr, avail')
            }
          (lspans, rspans)
    seq {
      for l in newLs do
        for r in newRs do
          yield { Left = l; Right = r }
    }

  let addStores (s1: Store) (s2: Store) : Option<Store> =
    let check1in2 (r: RegId) (v1: IntId * Map<FldId,Val>) : Bool =
      match Map.tryFind r s2 with
      | None -> true
      | Some v2 -> v1 = v2
    let mkSum (om: Option<Store>) (k: RegId) (v: IntId * Map<FldId,Val>) : Option<Store> =
      match om with
      | None   -> None
      | Some m -> if check1in2 k v then Some (Map.add k v m) else None
    Map.fold mkSum (Some s2) s1

  let updateStore (s1: Store) (s2: Store) : Store =
    Map.union s1 s2

  let importStore (m: Map<RegId,RegId>) (s: Store) =  
    let importValue v =
      match v with
      | VReg r -> VReg m.[r]
      | _      -> v
    let importFields fldMap =
      Map.map (fun _ v -> importValue v) fldMap
    Map.fold (fun t k (i,fldMap) -> Map.add m.[k] (i, importFields fldMap) t) Map.empty s

  let matchedMoves (sp: Span) (ms1: List<Move>) (ms2: List<Move>) : Bool =
    let mv (v1: Val) (v2: Val) =
      match v1, v2 with
      | VNum i,  VNum j  -> i=j
      | VStar,   VStar   -> true
      | VNul,    VNul    -> true
      | VReg r1, VReg r2 -> sp.Left.[r1] = sp.Right.[r2]
      | _,       _       -> false
    let mm (m1: Move) (m2: Move) =
      match m1, m2 with
      | ValM v1,              ValM v2              -> mv v1 v2
      | Call (r1, mth1, vs1), Call (r2, mth2, vs2) ->
          mth1 = mth2 && sp.Left.[r1] = sp.Right.[r2] && List.forall2 mv vs1 vs2
      | Ret (r1, mth1, v1),   Ret (r2, mth2, v2)   ->
          mth1 = mth2 && sp.Left.[r1] = sp.Right.[r2] && mv v1 v2
      | _,                    _                    -> false
    List.forall2 mm ms1 ms2

  /// Given the owning player `pl`, a starting store `s0` and two labels `l1 = (m1, s1)` 
  /// and `l2 = (m2, s2)` and a span `sp`: `isGoodSpan pl s0 l1 l2 sp` is: 
  ///   * `Some s` for store `s` when `m1` and `m2` are equal modulo `sp` and 
  ///                                 `s1` and `s2` are compatible (`pl` = `O`)
  ///                                 `s0[s1]` and `s0[s2]` are compatible (`pl` = `P`)
  ///                                 `s` is `s1` + `s2` under `sp`.
  ///   * `None`                 otherwise
  let isGoodSpan (pl: Player) (s0: Store) ((mu1, s1): Letter) ((mu2, s2): Letter) (sp: Span) : Option<Store> =
      if matchedMoves sp [mu1] [mu2] then
        let s1' = importStore sp.Left s1
        let s2' = importStore sp.Right s2
        match addStores s1' s2' with
        | None      -> None
        | Some s1s2 -> 
            match pl with
            | O -> Some s1s2
            | P ->
                let s1'' = updateStore s0 s1'
                let s2'' = updateStore s0 s2'
                match addStores s1'' s2'' with
                | None -> None
                | Some _ -> Some s1s2
      else
        None

  /// Given the number of registers `n` of the FPDRS, a starting span-state `q`, 
  /// a transition of the product automaton `t = (q1,tl,q2)` and the player `pl` owning 
  /// state `q2` in the product: `transFromTrans n q t pl` is the list of all
  /// pairs `(t', q')` in which `t'` is the FPDRS equivalent transition to `t` that 
  /// reaches FPDRS state `q'`.  
  let transFromTrans (n: Int) (lTags, rTags) (q: SpanState) ((q1,tl,q2): IMJ2ATransition) (pl: Player) : List<Trans * SpanState> =
    let collectGoodSpans l1 l2 acc sp =
      match isGoodSpan q.Owner q.Store l1 l2 sp with
      | None -> acc
      | Some s -> (sp, s)::acc
    let mkq2 sp st = { State = q2; Span = sp; Store = st; Owner = pl }
    let ranL = Map.codomain q.Span.Left
    let ranR = Map.codomain q.Span.Right
    let ran  = Set.union ranL ranR

    let fresh sp = 
      let ranL' = Map.codomain sp.Left
      let ranR' = Map.codomain sp.Right
      let ran'  = Set.union ranL' ranR'
      Set.difference ran' ran

    let divergeIfNecessary (tl1: MoveLabel) (tl2: MoveLabel) xs =
      match q.Owner with
      | O -> xs
      | P -> 
          match xs with
          | [] -> 
              let q1, q2 =
                match q.State with
                | Sim (q1,q2) -> (q1,q2)
                | _ -> failwith "Expected Sim state."
              let xs1 = Map.codomain q.Span.Left
              let xs2 = Map.codomain q.Span.Right
              let q1' = { q with State = Div1 q1; Store = Map.restrict q.Store xs1; Span = { q.Span with Right = Map.empty } }
              let q2' = { q with State = Div2 q2; Store = Map.restrict q.Store xs2; Span = { q.Span with Left = Map.empty } }
              let t1 = (q, Cut xs1, q1')
              let t2 = (q, Cut xs2, q2')
              [ (t1, q1'); (t2, q2') ]
          | _::_ -> xs

    let divTrans tags (tl: MoveLabel) mkSpan pi  =
      match tl with
      | NoopL (xs, l) ->
          let xs' = Set.toList xs
          let ran = Map.codomain (pi q.Span) 
          let avail = Set.difference (set {1..n}) ran
          let freshSpans = newHalfSpans (pi q.Span) xs' (Set.empty,avail)
          [
            for sp in freshSpans do
              let st = importStore sp (snd l) 
              let sp' = mkSpan sp
              let q' = { State = q2; Span = sp'; Store = st; Owner = pl }
              let x = fresh sp'
              yield ((q, Noop x, q'), q')
          ]
      | PushL (xs, l, p, y) ->
          let xs' = Set.toList xs
          let ran = Map.codomain (pi q.Span) 
          let avail = Set.difference (set {1..n}) ran
          let freshSpans = newHalfSpans (pi q.Span) xs' (Set.empty, avail)
          [
            for sp in freshSpans do
              let st = importStore sp (snd l)
              let sp' = mkSpan sp
              let q' = { State = q2; Span = sp'; Store = st; Owner = pl }
              let x = fresh sp'
              let y' = Set.map (fun x -> (pi q.Span).[x]) y
              yield ((q, Push1 (x, (p,y')), q'), q')
          ]
      | PopL (xs, l, p, y, z) ->
          let xs' = Set.toList xs
          let ran = Map.codomain (pi q.Span) 
          let avail = Set.difference (set {1..n}) ran
          let free = Set.difference y z
          let popSpans = newHalfSpans (pi q.Span) (Set.toList free) (Set.empty, avail)
          let freshSpans = [
              for m in popSpans do
                let freeRange = Set.map (fun r -> m.[r]) free
                yield! newHalfSpans m xs' (Set.empty,Set.difference avail freeRange)
            ]
          let divPops = [
            for sp in freshSpans do
              let st = importStore sp (snd l)
              let sp' = mkSpan sp
              let q' = { State = q2; Span = sp'; Store = st; Owner = pl }
              let fr = fresh sp'
              let y' = Set.map (fun r -> (pi sp').[r]) y
              yield ((q, Pop1 (fr, (p,y')), q'), q')
          ]
          let extraPops = [
            for t in tags do
              for i in [1..n] do
                yield ((q, Pop1 (Set.empty, (t,Set.singleton i)), q), q)
          ]
          let morePops = [
            for t in tags do
              yield ((q, PopFre t, q), q)
          ]
          morePops @ extraPops @ divPops

    match tl with
    | Move12 (tl1,tl2) ->
        match tl1, tl2 with
        | NoopL (xs1, l1), NoopL (xs2, l2) ->
            let freshSpans = newSpans n q.Owner xs1 xs2 q.Span
            let goodSpans = Seq.fold (collectGoodSpans l1 l2) [] freshSpans
            let newTrans = [            
                for (sp, st) in goodSpans do
                  let q' = mkq2 sp st
                  let x = fresh sp
                  yield ((q, Noop x, q'), q')
              ]
            divergeIfNecessary tl1 tl2 newTrans
        | PushL (xs1, l1, p1, y1), PushL (xs2, l2, p2, y2) ->
            let freshSpans = newSpans n q.Owner xs1 xs2 q.Span
            let goodSpans = Seq.fold (collectGoodSpans l1 l2) [] freshSpans
            let newTrans = [
                for (sp, st) in goodSpans do
                  let q' = mkq2 sp st
                  let x = fresh sp
                  let y1' = Set.map (fun x -> q.Span.Left.[x]) y1
                  let y2' = Set.map (fun x -> q.Span.Right.[x]) y2
                  yield ((q, Push (x, (p1,y1'), (p2,y2')), q'), q')
              ]
            divergeIfNecessary tl1 tl2 newTrans
        | PopL (xs1, l1, p1, y1, z1), PopL (xs2, l2, p2, y2, z2) ->
            let free1 = Set.difference y1 z1
            let free2 = Set.difference y2 z2
            let popSpans = newSpans n O free1 free2 q.Span
            let freshSpans = seq {
                for psp in popSpans do
                  yield! newSpans n q.Owner xs1 xs2 psp
              }
            let goodSpans = Seq.fold (collectGoodSpans l1 l2) [] freshSpans
            let newTrans = [
                for (sp, st) in goodSpans do
                  let q'  = mkq2 sp st
                  let fr  = fresh sp
                  let y1' = Set.map (fun r -> sp.Left.[r]) y1
                  let y2' = Set.map (fun r -> sp.Right.[r]) y2
                  yield ((q, Pop (fr, (p1,y1'),(p2,y2')), q'), q')
              ]
            divergeIfNecessary tl1 tl2 newTrans
        | _, _ -> divergeIfNecessary tl1 tl2 []

    | Move1 tl -> divTrans rTags tl (fun sp -> { Left = sp; Right = Map.empty }) (fun sp -> sp.Left)
    | Move2 tl -> divTrans lTags tl (fun sp -> { Left = Map.empty; Right = sp }) (fun sp -> sp.Right)

    | Set1 x
    | Set1Id x ->
        let rran = Map.codomain q.Span.Right 
        let x' = 
          set [
            for r in x do
              let r' = q.Span.Left.[r]
              yield r'
          ]
        let x'' = x' + rran
        let st  = Map.restrict q.Store x''
        let sp  = { q.Span with Left = Map.restrict q.Span.Left x }
        let q'  = { State = q2; Span = sp; Store = st; Owner = pl }
        [(q, Cut x'', q'), q']

    | Set2 x
    | SetId2 x ->
        let lran = Map.codomain q.Span.Left 
        let x' = 
          set [
            for r in x do
              let r' = q.Span.Right.[r]
              yield r'
          ]
        let x'' = x' + lran
        let st  = Map.restrict q.Store x''
        let sp  = { q.Span with Right = Map.restrict q.Span.Right x }
        let q'  = { State = q2; Span = sp; Store = st; Owner = pl }
        [(q, Cut x'', q'), q']

    | Perm1 pi
    | Perm1Id pi ->
        let sp = { q.Span with Left = Map.mapDom q.Span.Left (fun k -> pi.[k]) }
        let q' = { q with State = q2; Span = sp; Owner = pl }
        [(q, Eps, q'), q']

    | Perm2 pi
    | PermId2 pi ->
        let sp = { q.Span with Right = Map.mapDom q.Span.Right (fun k -> pi.[k]) }
        let q' = { q with State = q2; Span = sp; Owner = pl }
        [(q, Eps, q'), q']


  let fromIMJ2A (a: IMJ2A) : FPDRA =
    
    let lTags, rTags = 
      let getTags (lTags, rTags) (_,t: IMJ2ALabel,_) =
        match t with
        | Move1 (PushL(_,_,sc,_)) -> (Set.add sc lTags, rTags)
        | Move2 (PushL(_,_,sc,_)) -> (lTags, Set.add sc rTags)
        | _ -> (lTags, rTags)
      List.fold getTags (Set.empty,Set.empty) a.Trans

    let trans = HashSet ()
    let states = HashSet ()

    let rec fix (fr: List<SpanState>) : Unit =
      if not (List.isEmpty fr) then
        let newFr = [
            for q in fr do
              for _,_,q2 as t in IMJ2A.transFromState a q.State do
                let pl = a.Owner q2
                let news = transFromTrans a.NumRegs (lTags,rTags) q t pl
                for t',q' in news do
                  let _ = trans.Add t'
                  let notSeen = states.Add q'
                  if notSeen then yield q'
          ]
        fix newFr

    let st = snd a.InitC
    let regs = Store.supp st
    let deltaMap = Set.fold (fun m r -> Map.add r r m) Map.empty regs
    let sp = { Left = deltaMap; Right = deltaMap }
    let q0 = { State = a.InitS; Span = sp; Store = st; Owner = a.Owner a.InitS }
    let _ = states.Add q0
    do fix [q0]

    let finals = [ for q in states do if List.contains q.State a.Final then yield q ]

    {
      States = List.ofSeq states
      Finals = finals
      Transitions = List.ofSeq trans
      Initial = q0
      NumRegs = a.NumRegs
    }
      
