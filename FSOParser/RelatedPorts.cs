// Copyright (c) 2024 Robert Bosch GmbH
// All rights reserved.
//
// This source code is licensed under the MIT license found in the
// LICENSE file in the root directory of this source tree.

// The orginal code is by Ali Kücükavci from the RevitToRDFConverter https://github.com/Semantic-HVAC-Tool/Parser
// Under the MIT license


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.ApplicationServices;
//using RestSharp;
using System.Net.Http;
using Autodesk.Revit.DB.Analysis;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using System.Runtime.Remoting.Messaging;

namespace FSOParser
{
    class RelatedPorts
    {
        public static StringBuilder GenericConnectors(Pipe component, string revitID, string componentID, StringBuilder sb)
        {
            //Port type
            ConnectorSet connectorSet = component.ConnectorManager.Connectors;

            string connectorID;
            string connectorDirectionID;
            string connectorDirection;
            double connectorOuterDiameter;
            string connectedConnectorID;
            string connectedConnectorDirection;
            string connectorOuterDiameterID;
            string connectorWidthID;
            string connectorHeightID;
            double connectorWidth;
            double connectorHeight;
            string connectedComponentID;
            string connectorDirectionVectorZID;
            string connectorDirectionVectorZ;
            string crossSectionalAreaID;
            double crossSectionalArea;

            foreach (Connector connector in connectorSet)
            {

                if (Domain.DomainHvac == connector.Domain || Domain.DomainPiping == connector.Domain)
                {
                    //Type
                    connectorID = componentID + "-" + connector.Id;
                    sb.Append(
                        $"inst:Comp_{componentID} fso:hasPort inst:Port_{connectorID} ." + "\n"
                        + $"inst:Port_{connectorID} a fso:Port ." + "\n"
                    );

                    //FlowDirection
                    connectorDirectionID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                    connectorDirection = connector.Direction.ToString();
                    //connectorDirectionVectorZID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                    //connectorDirectionVectorZ = connector.CoordinateSystem.BasisZ.ToString();

                    sb.Append(
                        $"inst:Port_{connectorID} ssn:hasProperty inst:FlowDirection_{connectorDirectionID} ." + "\n"
                        + $"inst:FlowDirection_{connectorDirectionID} a ssn-ext:NominalFlowDirection ;" + "\n"
                        + $"\tbrick:value '{connectorDirection}'^^xsd:string ." + "\n"
                        //+ $"inst:Port_{connectorID} ex:hasFlowDirectionVectorZ inst:{connectorDirectionVectorZID} ." + "\n"
                        //+ $"inst:{connectorDirectionVectorZID} fpo:hasValue '{connectorDirectionVectorZ}'^^xsd:string ." + "\n"
                        );

                    //Size
                    if (connector.Shape.ToString() == "Round")
                    {
                        connectorOuterDiameterID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        connectorOuterDiameter = UnitUtils.ConvertFromInternalUnits(connector.Radius * 2, UnitTypeId.Meters);
                        sb.Append(
                            $"inst::Port_{connectorID} ssn:hasProperty inst:Diameter_{connectorOuterDiameterID} ." + "\n" +
                            $"inst:Diameter_{connectorOuterDiameterID} a ssn-ext:OuterDiameter ;" + "\n" +
                            $"\tbrick:value '{connectorOuterDiameter}'^^xsd:double ;" + "\n" +
                            $"\tbrick:hasUnit unit:M ." + "\n"
                        );

                        crossSectionalAreaID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        crossSectionalArea = Math.PI * Math.Pow(UnitUtils.ConvertFromInternalUnits(connector.Radius, UnitTypeId.Meters), 2);
                        sb.Append(
                            $"inst:Port_{connectorID} ssn:hasProperty inst:CrossSectionArea_{crossSectionalAreaID} ." + "\n" +
                            $"inst:CrossSectionArea_{crossSectionalAreaID} a ssn-ext:CrossSectionalArea ;" + "\n" +
                            $"\tbrick:value '{crossSectionalArea}'^^xsd:double ;" + "\n" +
                            $"\tbrick:hasUnit unit:M2 ." + "\n"
                        );

                    }
                    else if (connector.Shape.ToString() == "Rectangular")
                    {
                        connectorWidthID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        connectorHeightID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        connectorHeight = UnitUtils.ConvertFromInternalUnits(connector.Height, UnitTypeId.Meters);
                        connectorWidth = UnitUtils.ConvertFromInternalUnits(connector.Width, UnitTypeId.Meters);
                        sb.Append(
                            $"inst:Port_{connectorID} ssn:hasProperty inst:Height_{connectorHeightID} ." + "\n" +
                            $"inst:Height_{connectorHeightID} a ssn-ext:Height ;" + "\n" +
                            $"\tbrick:value '{connectorHeight}'^^xsd:double ;" + "\n" +
                            $"\tbrick:hasUnit unit:M ." + "\n" +
                            $"inst:Port_{connectorID} ssn:hasProperty inst:Width_{connectorWidthID} ." + "\n" +
                            $"inst:Width_{connectorWidthID} a ssn-ext:Width ;" + "\n" +
                            $"\tbrick:value '{connectorWidth}'^^xsd:double ;" + "\n" +
                            $"\tbrick:hasUnit unit:M ." + "\n"
                            );

                        crossSectionalAreaID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        crossSectionalArea = connectorHeight * connectorWidth;
                        sb.Append(
                            $"inst:Port_{connectorID} ssn:Property inst:CrossSectionalArea_{crossSectionalAreaID} ." + "\n" +
                            $"inst:CrossSectionalArea_{crossSectionalAreaID} a ssn-ext:CrossSectionalArea ;" + "\n" +
                            $"\tbrick:value '{crossSectionalArea}'^^xsd:double ;" + "\n" +
                            $"\tbrick:hasUnit unit:M ." + "\n"
                            );
                    }
                    //Pressure drop
                    if (component.LookupParameter("NominalPressureDrop") != null)
                    {
                        //Water side pressure drop
                        string pressureDropID = System.Guid.NewGuid().ToString().Replace(' ', '-'); ;
                        double pressureDropValue = UnitUtils.ConvertFromInternalUnits(component.LookupParameter("NominalPressureDrop").AsDouble(), UnitTypeId.Pascals);
                        sb.Append(
                         $"inst:{connectorID} ssn:hasProperty inst:PressureDrop_{pressureDropID} ." + "\n"
                       + $"inst:PressureDrop_{pressureDropID} a ssn-ext:NominalPressureDrop ;" + "\n"
                       + $"\tbrick:value '{pressureDropValue}'^^xsd:double ;" + "\n"
                       + $"\tbrick:hasUnit unit:PA ." + "\n"
                       );
                    }
                    //ReynoldsNumber
                    if (component.LookupParameter("ReynoldsNumber") != null)
                    {
                        //Reynolds number
                        string reynoldsID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        double reynoldsValue = component.LookupParameter("ReynoldsNumber").AsDouble();
                        sb.Append(
                           $"inst:Port_{connectorID} ssn:hasProperty inst:Re_{reynoldsID} ." + "\n"
                         + $"inst:Re_{reynoldsID} a ssn-ext:ReynoldsNumber ;" + "\n"
                         + $"\tbrick:value '{reynoldsValue}'^^xsd:double ." + "\n"
                         );
                    }
                    //FrictionFactor
                    if (component.LookupParameter("FrictionFactor") != null)
                    {
                        //frictionFactor 
                        string frictionFactorID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        double frictionFactorValue = component.LookupParameter("FrictionFactor").AsDouble();
                        sb.Append(
                            $"inst:{connectorID} ssn:hasProperty inst:FrictionFactor_{frictionFactorID} ." + "\n"
                         + $"inst:FrictionFactor_{frictionFactorID} a ssn-ext:FrictionFactor;" + "\n"
                         + $"\tbrick:value '{frictionFactorValue}'^^xsd:double ." + "\n"
                        );
                    }
                    //FlowState
                    if (component.LookupParameter("FlowState") != null)
                    {
                        //Flow State
                        string flowStateID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        string flowStateValue = component.LookupParameter("FlowState").AsValueString();
                        sb.Append(
                           $"inst:Port_{connectorID} ssn:hasProperty inst:FlowState_{flowStateID} ." + "\n"
                         + $"inst:FlowState_{flowStateID} a ssn-ext:FlowState ;" + "\n"
                         + $"\tbrick:value '{flowStateValue}'^^xsd:string ;" + "\n"
                         + $"\tbrick:hasUnit uni:PA ." + "\n"
                         );
                    }
                    //VolumeFlow
                    if (connector.Flow != null)
                    {
                        //Flow rate
                        string flowID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        double flowValue = UnitUtils.ConvertFromInternalUnits(connector.Flow, UnitTypeId.LitersPerSecond);
                        sb.Append($"inst:Port_{connectorID} ssn:hasProperty inst:VolumeFlow_{flowID} ." + "\n"
                         + $"inst:VolumeFlow_{flowID} a ssn-ext:NominalVolumeFlow;" + "\n"
                         + $"\tbrick:value '{flowValue}'^^xsd:double ;" + "\n"
                         + $"\tbrick:hasUnit unit:L-PER-SEC ." + "\n");
                    }
                    //Port relationship to other ports
                    ConnectorSet joinedconnectors = connector.AllRefs;
                    if (connectorDirection == "Out")
                    {
                        foreach (Connector connectedConnector in joinedconnectors)
                        {
                            connectedConnectorID = connectedConnector.Owner.UniqueId.ToString() + "-" + connectedConnector.Id.ToString();
                            connectedComponentID = connectedConnector.Owner.UniqueId.ToString();

                            if (connector.Owner.UniqueId != connectedConnector.Owner.UniqueId)
                            {
                                if (Domain.DomainHvac == connectedConnector.Domain && connector.DuctSystemType == DuctSystemType.SupplyAir
                            || (Domain.DomainPiping == connectedConnector.Domain && connector.PipeSystemType == PipeSystemType.SupplyHydronic))
                                {
                                    connectedConnectorDirection = connectedConnector.Direction.ToString();

                                    sb.Append(
                                        $"inst:Port_{connectorID} fso:suppliesFluidTo inst:Port_{connectedConnectorID} ." + "\n"
                                        + $"inst:Port_{connectedConnectorID} a fso:Port ." + "\n"
                                        + $"inst:Comp_{componentID} fso:feedsFluidTo inst:Comp_{connectedComponentID} ." + "\n");

                                }
                                if (Domain.DomainHvac == connectedConnector.Domain && connector.DuctSystemType == DuctSystemType.ReturnAir
                                || Domain.DomainPiping == connectedConnector.Domain && connector.PipeSystemType == PipeSystemType.ReturnHydronic)
                                {
                                    connectedConnectorDirection = connectedConnector.Direction.ToString();

                                    sb.Append(
                                        $"inst:Comp_{connectorID} fso:returnsFluidTo inst:Port_{connectedConnectorID} ." + "\n"
                                        + $"inst:Port_{connectedConnectorID} a fso:Port ." + "\n"
                                        + $"inst:Comp_{componentID} fso:feedsFluidTo inst:Comp_{connectedComponentID} ." + "\n"
                                        );
                                }
                            }
                        }

                    }
                }
            }
            return sb;
        }
        public static StringBuilder GenericConnectors(Duct component, string revitID, string componentID, StringBuilder sb)
        {
            //Port type
            ConnectorSet connectorSet = component.ConnectorManager.Connectors;

            string connectorID;
            string connectorDirectionID;
            string connectorDirection;
            double connectorOuterDiameter;
            string connectedConnectorID;
            string connectedConnectorDirection;
            string connectorOuterDiameterID;
            string connectorWidthID;
            string connectorHeightID;
            double connectorWidth;
            double connectorHeight;
            string connectedComponentID;
            string connectorDirectionVectorZID;
            string connectorDirectionVectorZ;
            string crossSectionalAreaID;
            double crossSectionalArea;

            foreach (Connector connector in connectorSet)
            {

                if (Domain.DomainHvac == connector.Domain || Domain.DomainPiping == connector.Domain)
                {
                    //Type
                    connectorID = componentID + "-" + connector.Id;
                    sb.Append(
                        $"inst:Comp_{componentID} fso:hasPort inst:Port_{connectorID} ." + "\n"
                        + $"inst:Port_{connectorID} a fso:Port ." + "\n"
                    );
                    
                    //FlowDirection
                    connectorDirectionID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                    connectorDirection = connector.Direction.ToString();
                    //connectorDirectionVectorZID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                    //connectorDirectionVectorZ = connector.CoordinateSystem.BasisZ.ToString();

                    sb.Append(
                        $"inst:Port_{connectorID} ssn:hasProperty inst:FlowDirection_{connectorDirectionID} ." + "\n"
                        + $"inst:FlowDirection_{connectorDirectionID} a ssn-ext:NominalFlowDirection ;" + "\n"
                        + $"\tbrick:value '{connectorDirection}'^^xsd:string ." + "\n"
                        //+ $"inst:Port_{connectorID} ex:hasFlowDirectionVectorZ inst:{connectorDirectionVectorZID} ." + "\n"
                        //+ $"inst:{connectorDirectionVectorZID} fpo:hasValue '{connectorDirectionVectorZ}'^^xsd:string ." + "\n"
                        );

                    //Size
                    if (connector.Shape.ToString() == "Round")
                    {
                        connectorOuterDiameterID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        connectorOuterDiameter = UnitUtils.ConvertFromInternalUnits(connector.Radius * 2, UnitTypeId.Meters);
                        sb.Append(
                            $"inst::Port_{connectorID} ssn:hasProperty inst:Diameter_{connectorOuterDiameterID} ." + "\n" +
                            $"inst:Diameter_{connectorOuterDiameterID} a ssn-ext:OuterDiameter ;" + "\n" +
                            $"\tbrick:value '{connectorOuterDiameter}'^^xsd:double ;" + "\n" +
                            $"\tbrick:hasUnit unit:M ." + "\n"
                        );

                        crossSectionalAreaID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        crossSectionalArea = Math.PI * Math.Pow(UnitUtils.ConvertFromInternalUnits(connector.Radius, UnitTypeId.Meters), 2);
                        sb.Append(
                            $"inst:Port_{connectorID} ssn:hasProperty inst:CrossSectionArea_{crossSectionalAreaID} ." + "\n" +
                            $"inst:CrossSectionArea_{crossSectionalAreaID} a ssn-ext:CrossSectionalArea ;" + "\n" +
                            $"\tbrick:value '{crossSectionalArea}'^^xsd:double ;" + "\n" +
                            $"\tbrick:hasUnit unit:M2 ." + "\n"
                        );

                    }
                    else if (connector.Shape.ToString() == "Rectangular")
                    {
                        connectorWidthID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        connectorHeightID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        connectorHeight = UnitUtils.ConvertFromInternalUnits(connector.Height, UnitTypeId.Meters);
                        connectorWidth = UnitUtils.ConvertFromInternalUnits(connector.Width, UnitTypeId.Meters);
                        sb.Append(
                            $"inst:Port_{connectorID} ssn:hasProperty inst:Height_{connectorHeightID} ." + "\n" +
                            $"inst:Height_{connectorHeightID} a ssn-ext:Height ;" + "\n" +
                            $"\tbrick:value '{connectorHeight}'^^xsd:double ;" + "\n" +
                            $"\tbrick:hasUnit unit:M ." + "\n" +
                            $"inst:Port_{connectorID} ssn:hasProperty inst:Width_{connectorWidthID} ." + "\n" +
                            $"inst:Width_{connectorWidthID} a ssn-ext:Width ;" + "\n" +
                            $"\tbrick:value '{connectorWidth}'^^xsd:double ;" + "\n" +
                            $"\tbrick:hasUnit unit:M ." + "\n"
                            );

                        crossSectionalAreaID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        crossSectionalArea = connectorHeight * connectorWidth;
                        sb.Append(
                            $"inst:Port_{connectorID} ssn:Property inst:CrossSectionalArea_{crossSectionalAreaID} ." + "\n" +
                            $"inst:CrossSectionalArea_{crossSectionalAreaID} a ssn-ext:CrossSectionalArea ;" + "\n" +
                            $"\tbrick:value '{crossSectionalArea}'^^xsd:double ;" + "\n" +
                            $"\tbrick:hasUnit unit:M ." + "\n"
                            );
                    }
                    //Pressure drop
                    if (component.LookupParameter("NominalPressureDrop") != null)
                    {
                        //Water side pressure drop
                        string pressureDropID = System.Guid.NewGuid().ToString().Replace(' ', '-'); ;
                        double pressureDropValue = UnitUtils.ConvertFromInternalUnits(component.LookupParameter("NominalPressureDrop").AsDouble(), UnitTypeId.Pascals);
                        sb.Append(
                         $"inst:{connectorID} ssn:hasProperty inst:PressureDrop_{pressureDropID} ." + "\n"
                       + $"inst:PressureDrop_{pressureDropID} a ssn-ext:NominalPressureDrop ;" + "\n"
                       + $"\tbrick:value '{pressureDropValue}'^^xsd:double ;" + "\n"
                       + $"\tbrick:hasUnit unit:PA ." + "\n"
                       );
                    }
                    //ReynoldsNumber
                    if (component.LookupParameter("ReynoldsNumber") != null)
                    {
                        //Reynolds number
                        string reynoldsID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        double reynoldsValue = component.LookupParameter("ReynoldsNumber").AsDouble();
                        sb.Append(
                           $"inst:Port_{connectorID} ssn:hasProperty inst:Re_{reynoldsID} ." + "\n"
                         + $"inst:Re_{reynoldsID} a ssn-ext:ReynoldsNumber ;" + "\n"
                         + $"\tbrick:value '{reynoldsValue}'^^xsd:double ." + "\n"
                         );
                    }
                    //FrictionFactor
                    if (component.LookupParameter("FrictionFactor") != null)
                    {
                        //frictionFactor 
                        string frictionFactorID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        double frictionFactorValue = component.LookupParameter("FrictionFactor").AsDouble();
                        sb.Append(
                            $"inst:{connectorID} ssn:hasProperty inst:FrictionFactor_{frictionFactorID} ." + "\n"
                         + $"inst:FrictionFactor_{frictionFactorID} a ssn-ext:FrictionFactor;" + "\n"
                         + $"\tbrick:value '{frictionFactorValue}'^^xsd:double ." + "\n"
                        );
                    }
                    //FlowState
                    if (component.LookupParameter("FlowState") != null)
                    {
                        //Flow State
                        string flowStateID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        string flowStateValue = component.LookupParameter("FlowState").AsValueString();
                        sb.Append(
                           $"inst:Port_{connectorID} ssn:hasProperty inst:FlowState_{flowStateID} ." + "\n"
                         + $"inst:FlowState_{flowStateID} a ssn-ext:FlowState ;" + "\n"
                         + $"\tbrick:value '{flowStateValue}'^^xsd:string ;" + "\n"
                         + $"\tbrick:hasUnit uni:PA ." + "\n"
                         );
                    }
                    //VolumeFlow
                    if (connector.Flow != null)
                    {
                        //Flow rate
                        string flowID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        double flowValue = UnitUtils.ConvertFromInternalUnits(connector.Flow, UnitTypeId.LitersPerSecond);
                        sb.Append($"inst:Port_{connectorID} ssn:hasProperty inst:VolumeFlow_{flowID} ." + "\n"
                         + $"inst:VolumeFlow_{flowID} a ssn-ext:NominalVolumeFlow;" + "\n"
                         + $"\tbrick:value '{flowValue}'^^xsd:double ;" + "\n"
                         + $"\tbrick:hasUnit unit:L-PER-SEC ." + "\n");
                    }
                    //Port relationship to other ports
                    ConnectorSet joinedconnectors = connector.AllRefs;
                    if (connectorDirection == "Out")
                    {
                        foreach (Connector connectedConnector in joinedconnectors)
                        {
                            connectedConnectorID = connectedConnector.Owner.UniqueId.ToString() + "-" + connectedConnector.Id.ToString();
                            connectedComponentID = connectedConnector.Owner.UniqueId.ToString();

                            if (connector.Owner.UniqueId != connectedConnector.Owner.UniqueId)
                            {
                                if (Domain.DomainHvac == connectedConnector.Domain && connector.DuctSystemType == DuctSystemType.SupplyAir
                            || (Domain.DomainPiping == connectedConnector.Domain && connector.PipeSystemType == PipeSystemType.SupplyHydronic))
                                {
                                    connectedConnectorDirection = connectedConnector.Direction.ToString();

                                    sb.Append(
                                        $"inst:Port_{connectorID} fso:suppliesFluidTo inst:Port_{connectedConnectorID} ." + "\n"
                                        + $"inst:Port_{connectedConnectorID} a fso:Port ." + "\n"
                                        + $"inst:Comp_{componentID} fso:feedsFluidTo inst:Comp_{connectedComponentID} ." + "\n");

                                }
                                if (Domain.DomainHvac == connectedConnector.Domain && connector.DuctSystemType == DuctSystemType.ReturnAir
                                || Domain.DomainPiping == connectedConnector.Domain && connector.PipeSystemType == PipeSystemType.ReturnHydronic)
                                {
                                    connectedConnectorDirection = connectedConnector.Direction.ToString();

                                    sb.Append(
                                        $"inst:Comp_{connectorID} fso:returnsFluidTo inst:Port_{connectedConnectorID} ." + "\n"
                                        + $"inst:Port_{connectedConnectorID} a fso:Port ." + "\n"
                                        + $"inst:Comp_{componentID} fso:feedsFluidTo inst:Comp_{connectedComponentID} ." + "\n"
                                        );
                                }

                                if (Domain.DomainHvac == connectedConnector.Domain && connector.DuctSystemType == DuctSystemType.ExhaustAir)
                                {
                                    connectedConnectorDirection = connectedConnector.Direction.ToString();

                                    sb.Append(
                                        $"inst:Comp_{connectorID} fso:returnsFluidTo inst:Port_{connectedConnectorID} ." + "\n"
                                        + $"inst:Port_{connectedConnectorID} a fso:Port ." + "\n"
                                        + $"inst:Comp_{componentID} fso:feedsFluidTo inst:Comp_{connectedComponentID} ." + "\n"
                                        );
                                }

                                if (Domain.DomainHvac == connectedConnector.Domain && connector.DuctSystemType == DuctSystemType.OtherAir)
                                {
                                    connectedConnectorDirection = connectedConnector.Direction.ToString();

                                    sb.Append(
                                        $"inst:Comp_{connectorID} fso:returnsFluidTo inst:Port_{connectedConnectorID} ." + "\n"
                                        + $"inst:Port_{connectedConnectorID} a fso:Port ." + "\n"
                                        + $"inst:Comp_{componentID} fso:feedsFluidTo inst:Comp_{connectedComponentID} ." + "\n"
                                        );
                                }
                            }
                        }
                    }
                }
            }
            return sb;
        }

        public static StringBuilder GenericConnectors(FlexDuct component, string revitID, string componentID, StringBuilder sb)
        {
            //Port type
            ConnectorSet connectorSet = component.ConnectorManager.Connectors;

            string connectorID;
            string connectorDirectionID;
            string connectorDirection;
            double connectorOuterDiameter;
            string connectedConnectorID;
            string connectedConnectorDirection;
            string connectorOuterDiameterID;
            string connectorWidthID;
            string connectorHeightID;
            double connectorWidth;
            double connectorHeight;
            string connectedComponentID;
            string connectorDirectionVectorZID;
            string connectorDirectionVectorZ;
            string crossSectionalAreaID;
            double crossSectionalArea;

            foreach (Connector connector in connectorSet)
            {

                if (Domain.DomainHvac == connector.Domain || Domain.DomainPiping == connector.Domain)
                {
                    //Type
                    connectorID = componentID + "-" + connector.Id;
                    sb.Append(
                        $"inst:Comp_{componentID} fso:hasPort inst:Port_{connectorID} ." + "\n"
                        + $"inst:Port_{connectorID} a fso:Port ." + "\n"
                    );

                    //FlowDirection
                    connectorDirectionID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                    connectorDirection = connector.Direction.ToString();
                    //connectorDirectionVectorZID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                    //connectorDirectionVectorZ = connector.CoordinateSystem.BasisZ.ToString();

                    sb.Append(
                        $"inst:Port_{connectorID} ssn:hasProperty inst:FlowDirection_{connectorDirectionID} ." + "\n"
                        + $"inst:FlowDirection_{connectorDirectionID} a ssn-ext:NominalFlowDirection ;" + "\n"
                        + $"\tbrick:value '{connectorDirection}'^^xsd:string ." + "\n"
                        //+ $"inst:Port_{connectorID} ex:hasFlowDirectionVectorZ inst:{connectorDirectionVectorZID} ." + "\n"
                        //+ $"inst:{connectorDirectionVectorZID} fpo:hasValue '{connectorDirectionVectorZ}'^^xsd:string ." + "\n"
                        );

                    //Size
                    if (connector.Shape.ToString() == "Round")
                    {
                        connectorOuterDiameterID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        connectorOuterDiameter = UnitUtils.ConvertFromInternalUnits(connector.Radius * 2, UnitTypeId.Meters);
                        sb.Append(
                            $"inst::Port_{connectorID} ssn:hasProperty inst:Diameter_{connectorOuterDiameterID} ." + "\n" +
                            $"inst:Diameter_{connectorOuterDiameterID} a ssn-ext:OuterDiameter ;" + "\n" +
                            $"\tbrick:value '{connectorOuterDiameter}'^^xsd:double ;" + "\n" +
                            $"\tbrick:hasUnit unit:M ." + "\n"
                        );

                        crossSectionalAreaID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        crossSectionalArea = Math.PI * Math.Pow(UnitUtils.ConvertFromInternalUnits(connector.Radius, UnitTypeId.Meters), 2);
                        sb.Append(
                            $"inst:Port_{connectorID} ssn:hasProperty inst:CrossSectionArea_{crossSectionalAreaID} ." + "\n" +
                            $"inst:CrossSectionArea_{crossSectionalAreaID} a ssn-ext:CrossSectionalArea ;" + "\n" +
                            $"\tbrick:value '{crossSectionalArea}'^^xsd:double ;" + "\n" +
                            $"\tbrick:hasUnit unit:M2 ." + "\n"
                        );

                    }
                    else if (connector.Shape.ToString() == "Rectangular")
                    {
                        connectorWidthID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        connectorHeightID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        connectorHeight = UnitUtils.ConvertFromInternalUnits(connector.Height, UnitTypeId.Meters);
                        connectorWidth = UnitUtils.ConvertFromInternalUnits(connector.Width, UnitTypeId.Meters);
                        sb.Append(
                            $"inst:Port_{connectorID} ssn:hasProperty inst:Height_{connectorHeightID} ." + "\n" +
                            $"inst:Height_{connectorHeightID} a ssn-ext:Height ;" + "\n" +
                            $"\tbrick:value '{connectorHeight}'^^xsd:double ;" + "\n" +
                            $"\tbrick:hasUnit unit:M ." + "\n" +
                            $"inst:Port_{connectorID} ssn:hasProperty inst:Width_{connectorWidthID} ." + "\n" +
                            $"inst:Width_{connectorWidthID} a ssn-ext:Width ;" + "\n" +
                            $"\tbrick:value '{connectorWidth}'^^xsd:double ;" + "\n" +
                            $"\tbrick:hasUnit unit:M ." + "\n"
                            );

                        crossSectionalAreaID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        crossSectionalArea = connectorHeight * connectorWidth;
                        sb.Append(
                            $"inst:Port_{connectorID} ssn:Property inst:CrossSectionalArea_{crossSectionalAreaID} ." + "\n" +
                            $"inst:CrossSectionalArea_{crossSectionalAreaID} a ssn-ext:CrossSectionalArea ;" + "\n" +
                            $"\tbrick:value '{crossSectionalArea}'^^xsd:double ;" + "\n" +
                            $"\tbrick:hasUnit unit:M ." + "\n"
                            );
                    }
                    //Pressure drop
                    if (component.LookupParameter("NominalPressureDrop") != null)
                    {
                        //Water side pressure drop
                        string pressureDropID = System.Guid.NewGuid().ToString().Replace(' ', '-'); ;
                        double pressureDropValue = UnitUtils.ConvertFromInternalUnits(component.LookupParameter("NominalPressureDrop").AsDouble(), UnitTypeId.Pascals);
                        sb.Append(
                         $"inst:{connectorID} ssn:hasProperty inst:PressureDrop_{pressureDropID} ." + "\n"
                       + $"inst:PressureDrop_{pressureDropID} a ssn-ext:NominalPressureDrop ;" + "\n"
                       + $"\tbrick:value '{pressureDropValue}'^^xsd:double ;" + "\n"
                       + $"\tbrick:hasUnit unit:PA ." + "\n"
                       );
                    }
                    //ReynoldsNumber
                    if (component.LookupParameter("ReynoldsNumber") != null)
                    {
                        //Reynolds number
                        string reynoldsID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        double reynoldsValue = component.LookupParameter("ReynoldsNumber").AsDouble();
                        sb.Append(
                           $"inst:Port_{connectorID} ssn:hasProperty inst:Re_{reynoldsID} ." + "\n"
                         + $"inst:Re_{reynoldsID} a ssn-ext:ReynoldsNumber ;" + "\n"
                         + $"\tbrick:value '{reynoldsValue}'^^xsd:double ." + "\n"
                         );
                    }
                    //FrictionFactor
                    if (component.LookupParameter("FrictionFactor") != null)
                    {
                        //frictionFactor 
                        string frictionFactorID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        double frictionFactorValue = component.LookupParameter("FrictionFactor").AsDouble();
                        sb.Append(
                            $"inst:{connectorID} ssn:hasProperty inst:FrictionFactor_{frictionFactorID} ." + "\n"
                         + $"inst:FrictionFactor_{frictionFactorID} a ssn-ext:FrictionFactor;" + "\n"
                         + $"\tbrick:value '{frictionFactorValue}'^^xsd:double ." + "\n"
                        );
                    }
                    //FlowState
                    if (component.LookupParameter("FlowState") != null)
                    {
                        //Flow State
                        string flowStateID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        string flowStateValue = component.LookupParameter("FlowState").AsValueString();
                        sb.Append(
                           $"inst:Port_{connectorID} ssn:hasProperty inst:FlowState_{flowStateID} ." + "\n"
                         + $"inst:FlowState_{flowStateID} a ssn-ext:FlowState ;" + "\n"
                         + $"\tbrick:value '{flowStateValue}'^^xsd:string ;" + "\n"
                         + $"\tbrick:hasUnit uni:PA ." + "\n"
                         );
                    }
                    //VolumeFlow
                    if (connector.Flow != null)
                    {
                        //Flow rate
                        string flowID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        double flowValue = UnitUtils.ConvertFromInternalUnits(connector.Flow, UnitTypeId.LitersPerSecond);
                        sb.Append($"inst:Port_{connectorID} ssn:hasProperty inst:VolumeFlow_{flowID} ." + "\n"
                         + $"inst:VolumeFlow_{flowID} a ssn-ext:NominalVolumeFlow;" + "\n"
                         + $"\tbrick:value '{flowValue}'^^xsd:double ;" + "\n"
                         + $"\tbrick:hasUnit unit:L-PER-SEC ." + "\n");
                    }
                    //Port relationship to other ports
                    ConnectorSet joinedconnectors = connector.AllRefs;
                    if (connectorDirection == "Out")
                    {
                        foreach (Connector connectedConnector in joinedconnectors)
                        {
                            connectedConnectorID = connectedConnector.Owner.UniqueId.ToString() + "-" + connectedConnector.Id.ToString();
                            connectedComponentID = connectedConnector.Owner.UniqueId.ToString();

                            if (connector.Owner.UniqueId != connectedConnector.Owner.UniqueId)
                            {
                                if (Domain.DomainHvac == connectedConnector.Domain && connector.DuctSystemType == DuctSystemType.SupplyAir
                            || (Domain.DomainPiping == connectedConnector.Domain && connector.PipeSystemType == PipeSystemType.SupplyHydronic))
                                {
                                    connectedConnectorDirection = connectedConnector.Direction.ToString();

                                    sb.Append(
                                        $"inst:Port_{connectorID} fso:suppliesFluidTo inst:Port_{connectedConnectorID} ." + "\n"
                                        + $"inst:Port_{connectedConnectorID} a fso:Port ." + "\n"
                                        + $"inst:Comp_{componentID} fso:feedsFluidTo inst:Comp_{connectedComponentID} ." + "\n");

                                }
                                if (Domain.DomainHvac == connectedConnector.Domain && connector.DuctSystemType == DuctSystemType.ReturnAir
                                || Domain.DomainPiping == connectedConnector.Domain && connector.PipeSystemType == PipeSystemType.ReturnHydronic)
                                {
                                    connectedConnectorDirection = connectedConnector.Direction.ToString();

                                    sb.Append(
                                        $"inst:Comp_{connectorID} fso:returnsFluidTo inst:Port_{connectedConnectorID} ." + "\n"
                                        + $"inst:Port_{connectedConnectorID} a fso:Port ." + "\n"
                                        + $"inst:Comp_{componentID} fso:feedsFluidTo inst:Comp_{connectedComponentID} ." + "\n"
                                        );
                                }

                                if (Domain.DomainHvac == connectedConnector.Domain && connector.DuctSystemType == DuctSystemType.ExhaustAir)
                                {
                                    connectedConnectorDirection = connectedConnector.Direction.ToString();

                                    sb.Append(
                                        $"inst:Comp_{connectorID} fso:returnsFluidTo inst:Port_{connectedConnectorID} ." + "\n"
                                        + $"inst:Port_{connectedConnectorID} a fso:Port ." + "\n"
                                        + $"inst:Comp_{componentID} fso:feedsFluidTo inst:Comp_{connectedComponentID} ." + "\n"
                                        );
                                }

                                if (Domain.DomainHvac == connectedConnector.Domain && connector.DuctSystemType == DuctSystemType.OtherAir)
                                {
                                    connectedConnectorDirection = connectedConnector.Direction.ToString();

                                    sb.Append(
                                        $"inst:Comp_{connectorID} fso:returnsFluidTo inst:Port_{connectedConnectorID} ." + "\n"
                                        + $"inst:Port_{connectedConnectorID} a fso:Port ." + "\n"
                                        + $"inst:Comp_{componentID} fso:feedsFluidTo inst:Comp_{connectedComponentID} ." + "\n"
                                        );
                                }
                            }
                        }
                    }
                }
            }
            return sb;
        }
        public static StringBuilder GenericConnectors(FamilyInstance component, string revitID, string componentID, StringBuilder sb)
        {
            //Port type
            ConnectorSet connectorSet = component.MEPModel.ConnectorManager.Connectors;

            string connectorID;
            string connectorDirectionID;
            string connectorDirection;
            double connectorOuterDiameter;
            string connectedConnectorID;
            string connectedConnectorDirection;
            string connectorOuterDiameterID;
            string connectorWidthID;
            string connectorHeightID;
            double connectorWidth;
            double connectorHeight;
            string connectedComponentID;
            string connectorDirectionVectorZID;
            string connectorDirectionVectorZ;
            string crossSectionalAreaID;
            double crossSectionalArea;

            foreach (Connector connector in connectorSet)
            {

                if (Domain.DomainHvac == connector.Domain || Domain.DomainPiping == connector.Domain)
                {
                    //Type
                    connectorID = componentID + "-" + connector.Id;
                    sb.Append(
                        $"inst:Comp_{componentID} fso:hasPort inst:Port_{connectorID} ." + "\n"
                        + $"inst:Port_{connectorID} a fso:Port ." + "\n"
                    );

                    //FlowDirection
                    connectorDirectionID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                    connectorDirection = connector.Direction.ToString();
                    //connectorDirectionVectorZID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                    //connectorDirectionVectorZ = connector.CoordinateSystem.BasisZ.ToString();

                    sb.Append(
                        $"inst:Port_{connectorID} ssn:hasProperty inst:FlowDirection_{connectorDirectionID} ." + "\n"
                        + $"inst:FlowDirection_{connectorDirectionID} a ssn-ext:NominalFlowDirection ;" + "\n"
                        + $"\tbrick:value '{connectorDirection}'^^xsd:string ." + "\n"
                        //+ $"inst:Port_{connectorID} ex:hasFlowDirectionVectorZ inst:{connectorDirectionVectorZID} ." + "\n"
                        //+ $"inst:{connectorDirectionVectorZID} fpo:hasValue '{connectorDirectionVectorZ}'^^xsd:string ." + "\n"
                        );

                    //Size
                    if (connector.Shape.ToString() == "Round")
                    {
                        connectorOuterDiameterID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        connectorOuterDiameter = UnitUtils.ConvertFromInternalUnits(connector.Radius * 2, UnitTypeId.Meters);
                        sb.Append(
                            $"inst::Port_{connectorID} ssn:hasProperty inst:Diameter_{connectorOuterDiameterID} ." + "\n" +
                            $"inst:Diameter_{connectorOuterDiameterID} a ssn-ext:OuterDiameter ;" + "\n" +
                            $"\tbrick:value '{connectorOuterDiameter}'^^xsd:double ;" + "\n" +
                            $"\tbrick:hasUnit unit:M ." + "\n"
                        );

                        crossSectionalAreaID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        crossSectionalArea = Math.PI * Math.Pow(UnitUtils.ConvertFromInternalUnits(connector.Radius, UnitTypeId.Meters), 2);
                        sb.Append(
                            $"inst:Port_{connectorID} ssn:hasProperty inst:CrossSectionArea_{crossSectionalAreaID} ." + "\n" +
                            $"inst:CrossSectionArea_{crossSectionalAreaID} a ssn-ext:CrossSectionalArea ;" + "\n" +
                            $"\tbrick:value '{crossSectionalArea}'^^xsd:double ;" + "\n" +
                            $"\tbrick:hasUnit unit:M2 ." + "\n"
                        );

                    }
                    else if (connector.Shape.ToString() == "Rectangular")
                    {
                        connectorWidthID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        connectorHeightID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        connectorHeight = UnitUtils.ConvertFromInternalUnits(connector.Height, UnitTypeId.Meters);
                        connectorWidth = UnitUtils.ConvertFromInternalUnits(connector.Width, UnitTypeId.Meters);
                        sb.Append(
                            $"inst:Port_{connectorID} ssn:hasProperty inst:Height_{connectorHeightID} ." + "\n" +
                            $"inst:Height_{connectorHeightID} a ssn-ext:Height ;" + "\n" +
                            $"\tbrick:value '{connectorHeight}'^^xsd:double ;" + "\n" +
                            $"\tbrick:hasUnit unit:M ." + "\n" +
                            $"inst:Port_{connectorID} ssn:hasProperty inst:Width_{connectorWidthID} ." + "\n" +
                            $"inst:Width_{connectorWidthID} a ssn-ext:Width ;" + "\n" +
                            $"\tbrick:value '{connectorWidth}'^^xsd:double ;" + "\n" +
                            $"\tbrick:hasUnit unit:M ." + "\n"
                            );

                        crossSectionalAreaID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        crossSectionalArea = connectorHeight * connectorWidth;
                        sb.Append(
                            $"inst:Port_{connectorID} ssn:Property inst:CrossSectionalArea_{crossSectionalAreaID} ." + "\n" +
                            $"inst:CrossSectionalArea_{crossSectionalAreaID} a ssn-ext:CrossSectionalArea ;" + "\n" +
                            $"\tbrick:value '{crossSectionalArea}'^^xsd:double ;" + "\n" +
                            $"\tbrick:hasUnit unit:M ." + "\n"
                            );
                    }
                    //Pressure drop
                    if (component.LookupParameter("NominalPressureDrop") != null)
                    {
                        //Water side pressure drop
                        string pressureDropID = System.Guid.NewGuid().ToString().Replace(' ', '-'); ;
                        double pressureDropValue = UnitUtils.ConvertFromInternalUnits(component.LookupParameter("NominalPressureDrop").AsDouble(), UnitTypeId.Pascals);
                        sb.Append(
                         $"inst:{connectorID} ssn:hasProperty inst:PressureDrop_{pressureDropID} ." + "\n"
                       + $"inst:PressureDrop_{pressureDropID} a ssn-ext:NominalPressureDrop ;" + "\n"
                       + $"\tbrick:value '{pressureDropValue}'^^xsd:double ;" + "\n"
                       + $"\tbrick:hasUnit unit:PA ." + "\n"
                       );
                    }
                    //ReynoldsNumber
                    if (component.LookupParameter("ReynoldsNumber") != null)
                    {
                        //Reynolds number
                        string reynoldsID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        double reynoldsValue = component.LookupParameter("ReynoldsNumber").AsDouble();
                        sb.Append(
                           $"inst:Port_{connectorID} ssn:hasProperty inst:Re_{reynoldsID} ." + "\n"
                         + $"inst:Re_{reynoldsID} a ssn-ext:ReynoldsNumber ;" + "\n"
                         + $"\tbrick:value '{reynoldsValue}'^^xsd:double ." + "\n"
                         );
                    }
                    //FrictionFactor
                    if (component.LookupParameter("FrictionFactor") != null)
                    {
                        //frictionFactor 
                        string frictionFactorID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        double frictionFactorValue = component.LookupParameter("FrictionFactor").AsDouble();
                        sb.Append(
                            $"inst:{connectorID} ssn:hasProperty inst:FrictionFactor_{frictionFactorID} ." + "\n"
                         + $"inst:FrictionFactor_{frictionFactorID} a ssn-ext:FrictionFactor;" + "\n"
                         + $"\tbrick:value '{frictionFactorValue}'^^xsd:double ." + "\n"
                        );
                    }
                    //FlowState
                    if (component.LookupParameter("FlowState") != null)
                    {
                        //Flow State
                        string flowStateID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        string flowStateValue = component.LookupParameter("FlowState").AsValueString();
                        sb.Append(
                           $"inst:Port_{connectorID} ssn:hasProperty inst:FlowState_{flowStateID} ." + "\n"
                         + $"inst:FlowState_{flowStateID} a ssn-ext:FlowState ;" + "\n"
                         + $"\tbrick:value '{flowStateValue}'^^xsd:string ;" + "\n"
                         + $"\tbrick:hasUnit uni:PA ." + "\n"
                         );
                    }
                    //VolumeFlow
                    if (connector.Flow != null)
                    {
                        //Flow rate
                        string flowID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        double flowValue = UnitUtils.ConvertFromInternalUnits(connector.Flow, UnitTypeId.LitersPerSecond);
                        sb.Append($"inst:Port_{connectorID} ssn:hasProperty inst:VolumeFlow_{flowID} ." + "\n"
                         + $"inst:VolumeFlow_{flowID} a ssn-ext:NominalVolumeFlow;" + "\n"
                         + $"\tbrick:value '{flowValue}'^^xsd:double ;" + "\n"
                         + $"\tbrick:hasUnit unit:L-PER-SEC ." + "\n");
                    }
                    //Port relationship to other ports
                    ConnectorSet joinedconnectors = connector.AllRefs;
                    if (connectorDirection == "Out")
                    {
                        foreach (Connector connectedConnector in joinedconnectors)
                        {

                            connectedConnectorID = connectedConnector.Owner.UniqueId.ToString() + "-" + connectedConnector.Id.ToString();
                            connectedComponentID = connectedConnector.Owner.UniqueId.ToString();

                            if (Domain.DomainHvac == connectedConnector.Domain && connector.DuctSystemType == DuctSystemType.SupplyAir
                            || (Domain.DomainPiping == connectedConnector.Domain && connector.PipeSystemType == PipeSystemType.SupplyHydronic))
                            {
                                connectedConnectorDirection = connectedConnector.Direction.ToString();

                                sb.Append(
                                    $"inst:Port_{connectorID} fso:suppliesFluidTo inst:Port_{connectedConnectorID} ." + "\n"
                                    + $"inst:Port_{connectedConnectorID} a fso:Port ." + "\n"
                                    + $"inst:Comp_{componentID} fso:feedsFluidTo inst:Comp_{connectedComponentID} ." + "\n");

                            }
                            if (Domain.DomainHvac == connectedConnector.Domain && connector.DuctSystemType == DuctSystemType.ReturnAir
                            || Domain.DomainPiping == connectedConnector.Domain && connector.PipeSystemType == PipeSystemType.ReturnHydronic)
                            {
                                connectedConnectorDirection = connectedConnector.Direction.ToString();

                                sb.Append(
                                    $"inst:Comp_{connectorID} fso:returnsFluidTo inst:Port_{connectedConnectorID} ." + "\n"
                                    + $"inst:Port_{connectedConnectorID} a fso:Port ." + "\n"
                                    + $"inst:Comp_{componentID} fso:feedsFluidTo inst:Comp_{connectedComponentID} ." + "\n"
                                    );
                            }

                            if (Domain.DomainHvac == connectedConnector.Domain && connector.DuctSystemType == DuctSystemType.ExhaustAir)
                            {
                                connectedConnectorDirection = connectedConnector.Direction.ToString();

                                sb.Append(
                                    $"inst:Comp_{connectorID} fso:returnsFluidTo inst:Port_{connectedConnectorID} ." + "\n"
                                    + $"inst:Port_{connectedConnectorID} a fso:Port ." + "\n"
                                    + $"inst:Comp_{componentID} fso:feedsFluidTo inst:Comp_{connectedComponentID} ." + "\n"
                                    );
                            }

                            if (Domain.DomainHvac == connectedConnector.Domain && connector.DuctSystemType == DuctSystemType.OtherAir)
                            {
                                connectedConnectorDirection = connectedConnector.Direction.ToString();

                                sb.Append(
                                    $"inst:Comp_{connectorID} fso:returnsFluidTo inst:Port_{connectedConnectorID} ." + "\n"
                                    + $"inst:Port_{connectedConnectorID} a fso:Port ." + "\n"
                                    + $"inst:Comp_{componentID} fso:feedsFluidTo inst:Comp_{connectedComponentID} ." + "\n"
                                    );
                            }



                        }

                    }
                }
            }
            return sb;
        }
    }
}
