﻿/*
   Copyright 2012-2020 Marco De Salvo

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

using RDFSharp.Query;

namespace RDFSharp.Model
{
    /// <summary>
    /// RDFDatatypeConstraint represents a SHACL constraint on the specified datatype for a given RDF term
    /// </summary>
    public class RDFDatatypeConstraint : RDFConstraint
    {

        #region Properties
        /// <summary>
        /// Allowed datatype for the given RDF term
        /// </summary>
        public RDFModelEnums.RDFDatatypes Datatype { get; internal set; }
        #endregion

        #region Ctors
        /// <summary>
        /// Default-ctor to build a named datatype constraint
        /// </summary>
        public RDFDatatypeConstraint(RDFResource constraintName, RDFModelEnums.RDFDatatypes datatype) : base(constraintName) {
            this.Datatype = datatype;
        }

        /// <summary>
        /// Default-ctor to build a blank datatype constraint
        /// </summary>
        public RDFDatatypeConstraint(RDFModelEnums.RDFDatatypes datatype) : this(new RDFResource(), datatype) { }
        #endregion

        #region Methods
        /// <summary>
        /// Evaluates this constraint against the given data graph
        /// </summary>
        internal override RDFValidationReport EvaluateConstraint(RDFShapesGraph shapesGraph,
                                                                 RDFShape shape,
                                                                 RDFGraph dataGraph,
                                                                 RDFResource focusNode,
                                                                 RDFPatternMember valueNode) {
            RDFValidationReport report = new RDFValidationReport(new RDFResource());
            switch (valueNode) {

                //Resource
                case RDFResource valueNodeResource:
                    report.AddResult(new RDFValidationResult(shape,
                                                             RDFVocabulary.SHACL.DATATYPE_CONSTRAINT_COMPONENT,
                                                             focusNode,
                                                             shape is RDFPropertyShape ? ((RDFPropertyShape)shape).Path : null,
                                                             valueNode,
                                                             shape.Messages,
                                                             new RDFResource(),
                                                             shape.Severity));
                    break;

                //PlainLiteral
                case RDFPlainLiteral valueNodePlainLiteral:
                    if (this.Datatype != RDFModelEnums.RDFDatatypes.XSD_STRING 
                            || (this.Datatype == RDFModelEnums.RDFDatatypes.XSD_STRING && !string.IsNullOrEmpty(valueNodePlainLiteral.Language))) {
                        report.AddResult(new RDFValidationResult(shape,
                                                                 RDFVocabulary.SHACL.DATATYPE_CONSTRAINT_COMPONENT,
                                                                 focusNode,
                                                                 shape is RDFPropertyShape ? ((RDFPropertyShape)shape).Path : null,
                                                                 valueNode,
                                                                 shape.Messages,
                                                                 new RDFResource(),
                                                                 shape.Severity));
                    }
                    break;

                //TypedLiteral
                case RDFTypedLiteral valueNodeTypedLiteral:
                    if (this.Datatype != valueNodeTypedLiteral.Datatype) {
                        report.AddResult(new RDFValidationResult(shape,
                                                                 RDFVocabulary.SHACL.DATATYPE_CONSTRAINT_COMPONENT,
                                                                 focusNode,
                                                                 shape is RDFPropertyShape ? ((RDFPropertyShape)shape).Path : null,
                                                                 valueNode,
                                                                 shape.Messages,
                                                                 new RDFResource(),
                                                                 shape.Severity));
                    }
                    break;

            }
            return report;
        }

        /// <summary>
        /// Gets a graph representation of this constraint
        /// </summary>
        internal override RDFGraph ToRDFGraph(RDFShape shape) {
            RDFGraph result = new RDFGraph();
            if (shape != null) {

                //sh:datatype
                result.AddTriple(new RDFTriple(shape, RDFVocabulary.SHACL.DATATYPE, new RDFResource(RDFModelUtilities.GetDatatypeFromEnum(this.Datatype))));

            }
            return result;
        }
        #endregion

    }
}