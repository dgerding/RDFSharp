﻿/*
   Copyright 2012-2019 Marco De Salvo

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

     http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using RDFSharp.Model;
using RDFSharp.Store;

namespace RDFSharp.Query {

    /// <summary>
    /// RDFSelectQuery is the SPARQL "SELECT" query implementation.
    /// </summary>
    public class RDFSelectQuery: RDFQuery {

        #region Properties
        /// <summary>
        /// Dictionary of projection variables and associated ordinals
        /// </summary>
        internal Dictionary<RDFVariable, Int32> ProjectionVars { get; set; }
        #endregion

        #region Ctors
        /// <summary>
        /// Default-ctor to build an empty SELECT query
        /// </summary>
        public RDFSelectQuery() {
            this.ProjectionVars = new Dictionary<RDFVariable, Int32>();
        }
        #endregion

        #region Interfaces
        /// <summary>
        /// Gives the string representation of the SELECT query
        /// </summary>
        public override String ToString() {
            StringBuilder query = new StringBuilder();

            // SELECT
            query.Append("SELECT");

            // DISTINCT
            this.GetModifiers().Where(mod => mod is RDFDistinctModifier)
                               .ToList()
                               .ForEach(dm => query.Append(" " + dm));

            // VARIABLES
            if (this.ProjectionVars.Any()) {
                this.ProjectionVars.OrderBy(x => x.Value).ToList().ForEach(variable => query.Append(" " + variable.Key));
            }
            else {
                query.Append(" *");
            }

            #region PATTERN GROUPS
            query.Append("\nWHERE {\n");
            Boolean printingUnion        = false;
            RDFPatternGroup lastQueryPG  = this.GetPatternGroups().LastOrDefault();
            foreach(var pg              in this.GetPatternGroups()) { 

                //Current pattern group is set as UNION with the next one
                if (pg.JoinAsUnion) {

                    //Current pattern group IS NOT the last of the query (so UNION keyword must be appended at last)
                    if (!pg.Equals(lastQueryPG)) {
                         //Begin a new Union block
                         if (!printingUnion) {
                              printingUnion = true;
                              query.Append("\n  {");
                         }
                         query.Append(pg.ToString(2) + "    UNION");
                    }

                    //Current pattern group IS the last of the query (so UNION keyword must not be appended at last)
                    else {
                         //End the Union block
                         if (printingUnion) {
                             printingUnion = false;
                             query.Append(pg.ToString(2));
                             query.Append("  }\n");
                         }
                         else {
                             query.Append(pg.ToString());
                         }
                    }

                }

                //Current pattern group is set as INTERSECT with the next one
                else {
                    //End the Union block
                    if (printingUnion) {
                        printingUnion = false;
                        query.Append(pg.ToString(2));
                        query.Append("  }\n");
                    }
                    else {
                        query.Append(pg.ToString());
                    }
                }

            }
            query.Append("\n}");
            #endregion

            #region MODIFIERS
            // ORDER BY
            if (this.GetModifiers().Any(mod     => mod is RDFOrderByModifier)) {
                query.Append("\nORDER BY");
                this.GetModifiers().Where(mod   => mod is RDFOrderByModifier)
                                   .ToList()
                                   .ForEach(om  => query.Append(" " + om));
            }

            // LIMIT/OFFSET
            if (this.GetModifiers().Any(mod     => mod is RDFLimitModifier || mod is RDFOffsetModifier)) {
                this.GetModifiers().Where(mod   => mod is RDFLimitModifier)
                                   .ToList()
                                   .ForEach(lim => query.Append("\n" + lim));
                this.GetModifiers().Where(mod   => mod is RDFOffsetModifier)
                                   .ToList()
                                   .ForEach(off => query.Append("\n" + off));
            }
            #endregion

            return query.ToString();
        }
        #endregion

        #region Methods
        /// <summary>
        /// Adds the given pattern group to the query
        /// </summary>
        public RDFSelectQuery AddPatternGroup(RDFPatternGroup patternGroup) {
            if (patternGroup != null) {
                if (!this.GetPatternGroups().Any(q => q.PatternGroupName.Equals(patternGroup.PatternGroupName, StringComparison.OrdinalIgnoreCase))) {
                     this.QueryMembers.Add(patternGroup);
                }
            }
            return this;
        }

        /// <summary>
        /// Adds the given variable to the results of the query
        /// </summary>
        public RDFSelectQuery AddProjectionVariable(RDFVariable projectionVariable) {
            if (projectionVariable != null) {
                if (!this.ProjectionVars.Any(pv => pv.Key.ToString().Equals(projectionVariable.ToString(), StringComparison.OrdinalIgnoreCase))) {
                     this.ProjectionVars.Add(projectionVariable, this.ProjectionVars.Count);
                }
            }
            return this;
        }

        /// <summary>
        /// Adds the given modifier to the SELECT query
        /// </summary>
        public RDFSelectQuery AddModifier(RDFModifier modifier) {
            if (modifier != null) {
            
                //Ensure to have only one distinct modifier in the query
                if (modifier is RDFDistinctModifier && this.GetModifiers().Any(m => m is RDFDistinctModifier)) {
                    return this;
                }
                //Ensure to have only one limit modifier in the query
                if (modifier is RDFLimitModifier    && this.GetModifiers().Any(m => m is RDFLimitModifier)) {
                    return this;
                }
                //Ensure to have only one offset modifier in the query
                if (modifier is RDFOffsetModifier   && this.GetModifiers().Any(m => m is RDFOffsetModifier)) {
                    return this;
                }
                //Ensure to have only one orderby modifier per variable in the query
                if (modifier is RDFOrderByModifier  && this.GetModifiers().Any(m => m is RDFOrderByModifier && ((RDFOrderByModifier)m).Variable.Equals(((RDFOrderByModifier)modifier).Variable))) {
                    return this;
                }

                //Add the modifier, avoiding duplicates
                if (!this.GetModifiers().Any(m => m.Equals(modifier))) {
                     this.QueryMembers.Add(modifier);
                }

            }
            return this;
        }

        /// <summary>
        /// Applies the query to the given graph 
        /// </summary>
        public RDFSelectQueryResult ApplyToGraph(RDFGraph graph) {
            if (graph != null) {
                return this.ApplyToDataSource(graph);
            }
            return new RDFSelectQueryResult();
        }

        /// <summary>
        /// Applies the query to the given store 
        /// </summary>
        public RDFSelectQueryResult ApplyToStore(RDFStore store) {
            if (store != null) {
                return this.ApplyToDataSource(store);
            }
            return new RDFSelectQueryResult();
        }

        /// <summary>
        /// Applies the query to the given federation
        /// </summary>
        public RDFSelectQueryResult ApplyToFederation(RDFFederation federation) {
            if (federation != null) {
                return this.ApplyToDataSource(federation);
            }
            return new RDFSelectQueryResult();
        }

        /// <summary>
        /// Applies the query to the given SPARQL endpoint
        /// </summary>
        public RDFSelectQueryResult ApplyToSPARQLEndpoint(RDFSPARQLEndpoint sparqlEndpoint) {
            RDFSelectQueryResult selResult = new RDFSelectQueryResult();
            if (sparqlEndpoint            != null) {
                RDFQueryEvents.RaiseSELECTQueryEvaluation(String.Format("Evaluating SELECT query on SPARQL endpoint '{0}'...", sparqlEndpoint));
				
				//Establish a connection to the given SPARQL endpoint
                using (WebClient webClient = new WebClient()) {

                    //Insert reserved "query" parameter
                    webClient.QueryString.Add("query", HttpUtility.UrlEncode(this.ToString()));

                    //Insert user-provided parameters
                    webClient.QueryString.Add(sparqlEndpoint.QueryParams);

                    //Insert request headers
                    webClient.Headers.Add(HttpRequestHeader.Accept, "application/sparql-results+xml");

                    //Send querystring to SPARQL endpoint
                    var sparqlResponse     = webClient.DownloadData(sparqlEndpoint.BaseAddress);

                    //Parse response from SPARQL endpoint
                    if (sparqlResponse    != null) {
                        using (var sStream = new MemoryStream(sparqlResponse)) {
                            selResult      = RDFSelectQueryResult.FromSparqlXmlResult(sStream);
                        }
                        selResult.SelectResults.TableName = this.ToString();
                    }

                }

                RDFQueryEvents.RaiseSELECTQueryEvaluation(String.Format("Evaluated SELECTQuery on SPARQL endpoint '{0}': Found '{1}' results.", sparqlEndpoint, selResult.SelectResultsCount));
            }
            return selResult;
        }

        /// <summary>
        /// Applies the query to the given datasource
        /// </summary>
        internal RDFSelectQueryResult ApplyToDataSource(RDFDataSource datasource) {
            this.PatternGroupResultTables.Clear();
            this.PatternResultTables.Clear();
            RDFQueryEvents.RaiseSELECTQueryEvaluation(String.Format("Evaluating SPARQL SELECT query on DataSource '{0}'...", datasource));

            RDFSelectQueryResult selResult      = new RDFSelectQueryResult();
            if (this.GetEvaluableMembers().Any())  {

                //Iterate the evaluable members of the query
                var fedPatternResultTables      = new Dictionary<Int64, List<DataTable>>();
                foreach (var evaluableMember   in this.GetEvaluableMembers()) {

                    #region PATTERN GROUP
                    if (evaluableMember        is RDFPatternGroup) {
                        RDFQueryEvents.RaiseSELECTQueryEvaluation(String.Format("Evaluating PatternGroup '{0}' on DataSource '{1}'...", (RDFPatternGroup)evaluableMember, datasource));

                        //Step 1: Get the intermediate result tables of the current pattern group
                        if (datasource.IsFederation()) {
                    
                            #region TrueFederations
                            foreach (var store in (RDFFederation)datasource) {

                                //Step FED.1: Evaluate the patterns of the current pattern group on the current store
                                RDFQueryEngine.EvaluatePatternGroup(this, (RDFPatternGroup)evaluableMember, store);

                                //Step FED.2: Federate the patterns of the current pattern group on the current store
                                if (!fedPatternResultTables.ContainsKey(evaluableMember.QueryMemberID)) {
                                     fedPatternResultTables.Add(evaluableMember.QueryMemberID, this.PatternResultTables[evaluableMember.QueryMemberID]);
                                }
                                else {
                                     fedPatternResultTables[evaluableMember.QueryMemberID].ForEach(fprt =>
                                        fprt.Merge(this.PatternResultTables[evaluableMember.QueryMemberID].Single(prt => prt.TableName.Equals(fprt.TableName, StringComparison.Ordinal)), true, MissingSchemaAction.Add));
                                }

                            }
                            this.PatternResultTables[evaluableMember.QueryMemberID] = fedPatternResultTables[evaluableMember.QueryMemberID];
                            #endregion

                        }
                        else {
                            RDFQueryEngine.EvaluatePatternGroup(this, (RDFPatternGroup)evaluableMember, datasource);
                        }

                        //Step 2: Get the result table of the current pattern group
                        RDFQueryEngine.FinalizePatternGroup(this, (RDFPatternGroup)evaluableMember);

                        //Step 3: Apply the filters of the current pattern group to its result table
                        RDFQueryEngine.ApplyFilters(this, (RDFPatternGroup)evaluableMember);
                    }
                    #endregion

                }

                //Step 4: Get the result table of the query
                var queryResultTable            = RDFQueryUtilities.CombineTables(this.PatternGroupResultTables.Values.ToList(), false);

                //Step 5: Apply the modifiers of the query to the result table
                selResult.SelectResults         = RDFQueryEngine.ApplyModifiers(this, queryResultTable);

            }
            RDFQueryEvents.RaiseSELECTQueryEvaluation(String.Format("Evaluated SPARQL SELECT query on DataSource '{0}': Found '{1}' results.", datasource, selResult.SelectResultsCount));

            selResult.SelectResults.TableName   = this.ToString();
            return selResult;
        }
        #endregion

    }

}