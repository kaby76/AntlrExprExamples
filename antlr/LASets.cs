﻿using System;
using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using Antlr4.Runtime.Misc;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace antlr
{
    class LASets
    {
        private Parser _parser;
        private AntlrInputStream _input_stream;
        private CommonTokenStream _token_stream;
        private List<IToken> _input;
        private int _cursor;
        private Dictionary<Pair<ATNState, int>, bool> _visited = new Dictionary<Pair<ATNState, int>, bool>();
        private HashSet<ATNState> _stop_states;
        private HashSet<ATNState> _start_states;
        private bool _log_parse = false;
        private bool _log_closure = false;
        private bool _log_first = false;

        class Edge
        {
            public ATNState _from;
            public ATNState _to;
            public ATNState _follow;
            public TransitionType _type;
            public IntervalSet _label;
            public int _index_at_transition;
            public int _index; // Where we are in parse at _to state.
        }


        public LASets()
        {
        }

        public IntervalSet Compute(Parser parser, CommonTokenStream token_stream, int line, int col)
        {
            _input = new List<IToken>();
            _parser = parser;
            _token_stream = token_stream;
            _stop_states = new HashSet<ATNState>();
            foreach (var s in parser.Atn.ruleToStopState.Select(t => parser.Atn.states[t.stateNumber]))
            {
                _stop_states.Add(s);
            }
            _start_states = new HashSet<ATNState>();
            foreach (var s in parser.Atn.ruleToStartState.Select(t => parser.Atn.states[t.stateNumber]))
            {
                _start_states.Add(s);
            }
            var currentIndex = _token_stream.Index;
            _token_stream.Seek(0);
            var offset = 1;
            while (true)
            {
                var token = _token_stream.LT(offset++);
                _input.Add(token);
                _cursor = token.TokenIndex;
                if (token.Type == TokenConstants.EOF)
                {
                    break;
                }
                if (token.Line >= line && token.Column >= col)
                {
                    break;
                }
            }

            var all_parses = EnterState(null);
            var result = new IntervalSet();
            if (all_parses != null)
            {
                foreach (var p in all_parses)
                {
                    HashSet<ATNState> set = ComputeSingle(p);
                    if (this._log_closure)
                        System.Console.Error.WriteLine("All states for path "
                                                       + String.Join(" ", set.ToList()));
                    foreach (var s in set)
                    {
                        foreach (var t in s.TransitionsArray)
                        {
                            switch (t.TransitionType)
                            {
                                case TransitionType.RULE:
                                    break;

                                case TransitionType.PREDICATE:
                                    break;

                                case TransitionType.WILDCARD:
                                    break;

                                default:
                                    if (!t.IsEpsilon)
                                        result.AddAll(t.Label);
                                    break;
                            }
                        }
                    }
                }
            }
            return result;
        }

        bool CheckPredicate(PredicateTransition transition) => transition.Predicate.Eval(this._parser, ParserRuleContext.EmptyContext);

        private int entry_value;

        // Step to state and continue parsing input.
        // Returns a list of transitions leading to a state that accepts input.
        List<List<Edge>> EnterState(Edge t)
        {
            int here = ++entry_value;

            int index_on_transition;
            int token_index;
            ATNState state;
            if (t == null)
            {
                token_index = 0;
                index_on_transition = 0;
                state = _parser.Atn.states[0];
            }
            else
            {
                token_index = t._index;
                index_on_transition = t._index_at_transition;
                state = t._to;
            }
            var input_token = _input[token_index];

            if (_log_parse)
                System.Console.Error.WriteLine("Entry " + here
                                    + " State " + state
                                    + " tokenIndex " + token_index
                                    + " " + input_token.Text
                                    );

            // Upon reaching the cursor, return match.
            var at_match = input_token.TokenIndex >= _cursor;
            if (at_match)
            {
                if (_log_parse)
                    System.Console.Error.Write("Entry " + here
                                         + " return ");
                var res = new List<List<Edge>>() { new List<Edge>() { t } };
                if (_log_parse)
                {
                    var str = PrintResult(res);
                    System.Console.Error.WriteLine(str);
                }
                return res;
            }

            if (_visited.ContainsKey(new Pair<ATNState, int>(state, token_index)))
                return null;

            _visited[new Pair<ATNState, int>(state, token_index)] = true;

            var result = new List<List<Edge>>();

            if (this._stop_states.Contains(state))
            {
                if (_log_parse)
                    System.Console.Error.Write("Entry " + here
                                              + " return ");
                var res = new List<List<Edge>>() { new List<Edge>() { t } };
                if (_log_parse)
                {
                    var str = PrintResult(res);
                    System.Console.Error.WriteLine(str);
                }
                return res;
            }

            // Search all transitions from state.
            foreach (Transition transition in state.TransitionsArray)
            {
                List<List<Edge>> matches = null;
                switch (transition.TransitionType)
                {
                    case TransitionType.RULE:
                        {
                            var rule = (RuleTransition)transition;
                            var sub_state = rule.target;
                            matches = this.EnterState(new Edge()
                            {
                                _from = state,
                                _to = rule.target,
                                _follow = rule.followState,
                                _label = rule.Label,
                                _type = rule.TransitionType,
                                _index = token_index,
                                _index_at_transition = token_index
                            });
                            if (matches != null && matches.Count == 0) throw new Exception();
                            if (matches != null)
                            {
                                List<List<Edge>> new_matches = new List<List<Edge>>();
                                foreach (var match in matches)
                                {
                                    var f = match.First(); // "to" is possibly final state of submachine.
                                    var l = match.Last(); // "to" is start state of submachine.
                                    var is_final = this._stop_states.Contains(f._to);
                                    var is_at_caret = f._index >= _cursor;
                                    if (!is_final)
                                        new_matches.Add(match);
                                    else
                                    {
                                        var xxx = this.EnterState(new Edge()
                                        {
                                            _from = f._to,
                                            _to = rule.followState,
                                            _label = null,
                                            _type = TransitionType.EPSILON,
                                            _index = f._index,
                                            _index_at_transition = f._index
                                        });
                                        if (xxx != null && xxx.Count == 0) throw new Exception();
                                        if (xxx != null)
                                        {
                                            foreach (var y in xxx)
                                            {
                                                var copy = y.ToList();
                                                foreach (var q in match)
                                                {
                                                    copy.Add(q);
                                                }
                                                new_matches.Add(copy);
                                            }

                                        }
                                    }
                                }
                                matches = new_matches;
                            }
                        }
                        break;

                    case TransitionType.PREDICATE:
                        if (this.CheckPredicate((PredicateTransition)transition))
                        {
                            matches = this.EnterState(new Edge()
                            {
                                _from = state,
                                _to = transition.target,
                                _label = transition.Label,
                                _type = transition.TransitionType,
                                _index = token_index,
                                _index_at_transition = token_index
                            });
                            if (matches != null && matches.Count == 0) throw new Exception();
                        }
                        break;

                    case TransitionType.WILDCARD:
                        matches = this.EnterState(new Edge()
                        {
                            _from = state,
                            _to = transition.target,
                            _label = transition.Label,
                            _type = transition.TransitionType,
                            _index = token_index + 1,
                            _index_at_transition = token_index
                        });
                        if (matches != null && matches.Count == 0) throw new Exception();
                        break;

                    default:
                        if (transition.IsEpsilon)
                        {
                            matches = this.EnterState(new Edge()
                            {
                                _from = state,
                                _to = transition.target,
                                _label = transition.Label,
                                _type = transition.TransitionType,
                                _index = token_index,
                                _index_at_transition = token_index
                            });
                            if (matches != null && matches.Count == 0) throw new Exception();
                        }
                        else
                        {
                            var set = transition.Label;
                            if (set != null && set.Count > 0)
                            {
                                if (transition.TransitionType == TransitionType.NOT_SET)
                                    set = set.Complement(IntervalSet.Of(TokenConstants.MinUserTokenType, _parser.Atn.maxTokenType));
                                if (set.Contains(input_token.Type))
                                {
                                    matches = this.EnterState(new Edge()
                                    {
                                        _from = state,
                                        _to = transition.target,
                                        _label = transition.Label,
                                        _type = transition.TransitionType,
                                        _index = token_index + 1,
                                        _index_at_transition = token_index
                                    });
                                    if (matches != null && matches.Count == 0) throw new Exception();
                                }
                            }
                        }
                        break;
                }

                if (matches != null)
                {
                    foreach (List<Edge> match in matches)
                    {
                        var x = match.ToList();
                        if (t != null)
                        {
                            x.Add(t);
                            Edge prev = null;
                            foreach (var z in x)
                            {
                                var ff = z._to;
                                if (prev != null)
                                    if (prev._from != ff)
                                    {
                                        System.Console.Error.WriteLine("Fail " + PrintSingle(x));
                                        Debug.Assert(false);
                                    }
                                prev = z;
                            }
                        }
                        result.Add(x);
                    }
                }
            }
            if (result.Count == 0)
                return null;

            if (_log_parse)
            {
                System.Console.Error.Write("Entry " + here
                                              + " return ");
                var str = PrintResult(result);
                System.Console.Error.WriteLine(str);
            }
            return result;
        }

        HashSet<ATNState> closure(ATNState start)
        {
            var visited = new HashSet<ATNState>();
            var stack = new Stack<ATNState>();
            stack.Push(start);
            while (stack.Any())
            {
                var state = stack.Pop();
                if (visited.Contains(state))
                    continue;
                visited.Add(state);
                foreach (var transition in state.TransitionsArray)
                {
                    List<List<Edge>> matches = null;
                    switch (transition.TransitionType)
                    {
                        case TransitionType.RULE:
                            {
                                var rule = (RuleTransition)transition;
                                var sub_state = rule.target;
                                var cl = closure(sub_state);
                                if (cl.Where(s => this._stop_states.Contains(s) && s.atn == sub_state.atn).Any())
                                {
                                    var cl2 = closure(rule.followState);
                                    cl.UnionWith(cl2);
                                }
                                foreach (var c in cl) visited.Add(c);
                            }
                            break;

                        case TransitionType.PREDICATE:
                            if (this.CheckPredicate((PredicateTransition)transition))
                            {
                                stack.Push(transition.target);
                            }
                            break;

                        case TransitionType.WILDCARD:
                            break;

                        default:
                            if (transition.IsEpsilon)
                                stack.Push(transition.target);
                            break;
                    }
                }
            }
            return visited;
        }

        HashSet<ATNState> ComputeSingle(List<Edge> parse)
        {
            var copy = parse.ToList();
            HashSet<ATNState> result = new HashSet<ATNState>();
            if (_log_closure)
            {
                System.Console.Error.WriteLine("Computing closure for the following parse:");
                System.Console.Error.Write(PrintSingle(parse));
                System.Console.Error.WriteLine();
            }

            if (!copy.Any())
                return result;
            var last_transaction = copy.First();
            var current_state = last_transaction._to;
            for (; ; )
            {
                if (_log_closure)
                {
                    System.Console.Error.WriteLine("Getting closure of " + current_state.stateNumber);
                }
                var c = closure(current_state);
                if (_log_closure)
                {
                    System.Console.Error.WriteLine("closure " + String.Join(" ", c.Select(s => s.stateNumber)));
                }
                bool do_continue = false;
                var atn = current_state.atn;
                var rule = current_state.ruleIndex;
                var start_state = atn.ruleToStartState[rule];
                var stop_state = atn.ruleToStopState[rule];
                foreach (var s in c)
                {
                    result.Add(s);
                    if (s == stop_state)
                        do_continue = true;
                }

                if (!do_continue)
                    break;

                for (; ; )
                {
                    copy.RemoveAt(0);
                    if (!copy.Any()) break;
                    last_transaction = copy.First();
                    if (start_state == last_transaction._from)
                    {
                        copy.RemoveAt(0);
                        last_transaction = copy.First();
                        // Get follow state of rule-type transition.
                        var from_state = last_transaction._from;
                        var follow_state = last_transaction._follow;
                        current_state = follow_state;
                        break;
                    }
                }
            }
            return result;
        }

        string PrintSingle(List<Edge> parse)
        {
            StringBuilder sb = new StringBuilder();
            var q = parse.ToList();
            q.Reverse();
            foreach (var t in q)
            {
                var sym = "";
                switch (t._type)
                {
                    case TransitionType.ACTION:
                        sym = "on action (eps)";
                        break;
                    case TransitionType.ATOM:
                        sym = "on " + t._label.ToString() + " ('" + this._input[t._index_at_transition].Text + "')";
                        break;
                    case TransitionType.EPSILON:
                        sym = "on eps";
                        break;
                    case TransitionType.INVALID:
                        sym = "invalid (eps)";
                        break;
                    case TransitionType.NOT_SET:
                        sym = "on not " + t._label.ToString();
                        break;
                    case TransitionType.PRECEDENCE:
                        sym = "on prec (eps)";
                        break;
                    case TransitionType.PREDICATE:
                        sym = "on pred (eps)";
                        break;
                    case TransitionType.RANGE:
                        sym = "on " + t._label.ToString() + " ('" + this._input[t._index_at_transition].Text + "')";
                        break;
                    case TransitionType.RULE:
                        sym = "on " + _parser.RuleNames[t._to.ruleIndex] + " (eps)";
                        break;
                    case TransitionType.SET:
                        sym = "on " + t._label.ToString() + " ('" + this._input[t._index_at_transition].Text + "')";
                        break;
                    case TransitionType.WILDCARD:
                        sym = "on wildcard ('" + this._input[t._index_at_transition].Text + "')";
                        break;
                    default:
                        break;
                }
                sb.Append(" / " + t._from + " => " + t._to + " " + sym);
            }
            return sb.ToString();
        }

        string PrintResult(List<List<Edge>> all_parses)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var p in all_parses)
            {
                sb.Append("||| " + PrintSingle(p));
            }
            return sb.ToString();
        }
    }
}
