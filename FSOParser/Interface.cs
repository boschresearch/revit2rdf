// Copyright (c) 2024 Robert Bosch GmbH
// All rights reserved.
//
// This source code is licensed under the MIT license found in the
// LICENSE file in the root directory of this source tree.

using System;
using System.Collections.Generic;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB.IFC;

//using RestSharp;
using System.Net.Http;
using Autodesk.Revit.DB.Analysis;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using System.Xml.Linq;
using System.Linq;

namespace FSOParser
{


    class Interface
    {
        public static StringBuilder DetailedGeometry(Document doc, StringBuilder sb)
        {
            //Code switches for speicifying the U-value and C-value
            var uSwitch = false;
            var cSwitch = false;
            var parameterSwitch = false;
            //Get the energy analysis model, if no existing model then a new model will be created
            EnergyAnalysisDetailModel eadm = EnergyAnalysisDetailModel.GetMainEnergyAnalysisDetailModel(doc);
            if (eadm == null)
            {
                // Collect space and surface data from the building's analytical thermal model
                EnergyAnalysisDetailModelOptions options = new EnergyAnalysisDetailModelOptions();
                options.Tier = EnergyAnalysisDetailModelTier.Final; // include constructions, schedules, and non-graphical data in the computation of the energy analysis model
                options.EnergyModelType = EnergyModelType.SpatialElement;   // Energy model based on rooms or spaces
                eadm = EnergyAnalysisDetailModel.Create(doc, options); // Create a new energy analysis detailed model from the physical model
            }


            // Create a filtered element collector for EnergyAnalysisSurface instances
            //FilteredElementCollector collector = new FilteredElementCollector(doc);
            //collector.OfClass(typeof(EnergyAnalysisSurface));

            IList<EnergyAnalysisSurface> surfaces = eadm.GetAnalyticalSurfaces();
            // Iterate over the collected elements
            foreach (EnergyAnalysisSurface energyAnalysisSurface in surfaces)
            {
                // Process each EnergyAnalysisSurface instance, and the original surface that is related to.
                Element surface = energyAnalysisSurface as Element;
                string surfaceID = energyAnalysisSurface.UniqueId;
                Element spatialSurfaceElement = doc.GetElement(energyAnalysisSurface.CADObjectUniqueId) as Element;
                string spatialSurfaceElementId = spatialSurfaceElement == null ? ElementId.InvalidElementId.ToString() : spatialSurfaceElement.UniqueId;
                string surfaceType = energyAnalysisSurface.SurfaceType.ToString();
                /*
                 * Member name 	Description
                    Roof 	        Roof.
                    ExteriorWall 	Exterior wall.
                    InteriorWall 	Interior wall.
                    Ceiling 	    Ceiling.
                    InteriorFloor 	Interior floor.
                    ExteriorFloor 	Exterior floor.
                    Shading 	    Shading.
                    Air 	        Air.
                    Underground 	Underground 
                 */
                double surfaceHeight = Math.Round(UnitUtils.ConvertFromInternalUnits(energyAnalysisSurface.Height, UnitTypeId.Meters), 2, MidpointRounding.ToEven);
                double surfaceWidth = Math.Round(UnitUtils.ConvertFromInternalUnits(energyAnalysisSurface.Width, UnitTypeId.Meters), 2, MidpointRounding.ToEven);
                double surfaceTilt = Math.Round(UnitUtils.ConvertFromInternalUnits(energyAnalysisSurface.Tilt, UnitTypeId.Radians), 2, MidpointRounding.ToEven);
                double surfaceArea = Math.Round(surfaceHeight * surfaceWidth, 2, MidpointRounding.ToEven);
                // Split the name of the analytical surface by "-"
                string[] nameParts = energyAnalysisSurface.Name.Split('-');
                // Take the first part of the name, as the orientation symbol
                string surfaceOrientation = nameParts.Length > 0 ? nameParts[0].Trim() : "Unknown";
                string opposite_orientation = "None";
                if (surfaceOrientation == "N")
                {
                    opposite_orientation = "S";
                }
                else if (surfaceOrientation == "S")
                {
                    opposite_orientation = "N";
                }
                else if (surfaceOrientation == "E")
                {
                    opposite_orientation = "W";
                }
                else if (surfaceOrientation == "W")
                {
                    opposite_orientation = "E";
                }
                else if (surfaceOrientation == "X")
                {
                    opposite_orientation = "X";
                }
                /*
                 * Surface and Opening elements get an Name element assigned according to the below described schema:

                (Orientation)(Space#)[(Other space#)](Exposure)(Type)-(sequence number)[Opening Type+#]

                Sample: N-101-102-E-W-O-77

                    N = Orientation [N/NE/E/SE/S/SW/W/NW/N/T/B/X] (every surface within the sector of 22.5 degrees from the north vector gets the letter N etc) (horizontal surfaces facing upwards get the letter T for top, downwards B for bottom) (shading surfaces get the letter X for differentiation).
                    101 = Space number.
                    102 = Other space number.
                    E = Exposure - exterior/interior/underground [E/I/U].
                    W = Type [W/C/R/F] (Wall, Roof, Ceiling, Floor, Shade) (every surface type has it's letter W-Wall R-Roof C-Ceiling F-Floor S-Shade).
                    O = Opening Type [W/D/O] (Window, Door, Opening) (every opening type has it's letter W-Window D-Door O-Opening).
                    77 = sequence number.

                Sample surface names:

                    N-101-E-W-84 North facing Exterior Wall #84 in space 101.
                    N-101-E-W-84-D-1 Door #1 in North facing Exterior Wall #84 in space 101.
                    E-101-102-I-W-92 Vertical Interior Wall #92 between space 101 and 102.
                    T-101-E-R-141 Top facing Exterior Roof #141 in space 101.
                    B-101-201-I-F-88 Bottom facing Interior Floor #88 between space 101 and 201.
                    X-73 Shade #73.
                 */
                sb.Append(
                   $"inst:Interface_{surfaceID}_1 a bot:Interface;" + "\n"
                + $"\tprops:hasBoundary '{surfaceType}'^^xsd:string;" + "\n"
                + $"\tprops:hasHeight '{surfaceHeight} m';" + "\n"
                + $"\tprops:hasWidth '{surfaceWidth} m';" + "\n"
                + $"\tprops:hasArea '{surfaceArea} m2';" + "\n"
                + $"\tprops:hasOrientation '{surfaceOrientation}'^^xsd:string;" + "\n"
                + $"\tprops:hasTilt '{surfaceTilt} rad';" + "\n"
                + $"\tprops:hasRevitId '{surface.Id.ToString()}'^^xsd:string ." + "\n"
                );

                if (spatialSurfaceElement != null)
                {
                    if (surfaceType == "Ceiling")
                    {
                        sb.Append(
                            $"inst:Element_{spatialSurfaceElementId} a beo:Covering-CEILING." + "\n"
                            );
                    }
                    else if (surfaceType == "Roof")
                    {
                        sb.Append(
                            $"inst:Element_{spatialSurfaceElementId} a beo:Roof." + "\n"
                            );
                    }
                    else if (surfaceType == "ExteriorWall" || surfaceType == "InteriorWall")
                    {
                        sb.Append(
                            $"inst:Element_{spatialSurfaceElementId} a beo:Wall." + "\n"
                            );
                    }
                    else if (surfaceType == "ExteriorFloor" || surfaceType == "InteriorFloor")
                    {
                        sb.Append(
                            $"inst:Element_{spatialSurfaceElementId} a beo:Slab-FLOOR." + "\n"
                            );
                    }
                    else if (surfaceType == "Shading")
                    {
                        sb.Append(
                            $"inst:Element_{spatialSurfaceElementId} a beo:Shading." + "\n"
                            );
                    }
                    else if (surfaceType == "Underground")
                    {
                        sb.Append(
                            $"inst:Element_{spatialSurfaceElementId} a beo:Slab." + "\n"
                            );
                    }
                    else
                    {
                        sb.Append(
                            $"inst:Element_{spatialSurfaceElementId} a beo:BuildingElement." + "\n"
                            );
                    }

                    sb.Append(
                  $"inst:Interface_{surfaceID}_1 bot:interfaceOf inst:Element_{spatialSurfaceElementId}." + "\n"
                + $"inst:Element_{spatialSurfaceElementId} rdfs:label '{spatialSurfaceElement.Name.Replace(' ', '_')}'^^xsd:string ;" + "\n"
                + $"\tprops:hasRevitId '{spatialSurfaceElement.Id.ToString()}'^^xsd:string ." + "\n"
                );
                    // Get the parameter set of the element
                    ParameterSet parameterSet = spatialSurfaceElement.Parameters;
                    if (parameterSwitch)
                    {
                        foreach (Parameter parameter in parameterSet)
                        {
                            // Get the parameter name and value
                            string parameterName = parameter.Definition.Name.Replace(' ', '_');
                            string parameterValue = Interface.GetParameterValue(parameter);

                            // Process each property
                            // For example, you can access its properties or perform actions
                            // In this example, we print the parameter name and value
                            if (parameterValue != null)
                            {
                                sb.Append(
                                $"inst:Element_{spatialSurfaceElementId} props:{parameterName} '{parameterValue.Replace(' ', '_')}'^^xsd:string ." + "\n"
                                );
                            }
                        }
                    }
                }

                if (surface.LookupParameter("HeatTransferCoefficient (U)") != null && uSwitch)
                {
                    double surfaceUvalue = Math.Round(UnitUtils.ConvertFromInternalUnits(surface.LookupParameter("HeatTransferCoefficient (U)").AsDouble(), UnitTypeId.WattsPerSquareMeterKelvin), 2, MidpointRounding.ToEven);
                    sb.Append($"inst:Interface_{surfaceID}_1 props:hasUvalue '{surfaceUvalue} W/(m2*K)'." + "\n");
                };

                if (surface.LookupParameter("Thermal Mass") != null && cSwitch)
                {
                    double surfaceCvalue = Math.Round(UnitUtils.ConvertFromInternalUnits(surface.LookupParameter("Thermal Mass").AsDouble(), UnitTypeId.JoulesPerSquareMeterKelvin), 2, MidpointRounding.ToEven);
                    sb.Append($"inst:Interface_{surfaceID}_1 props:hasCvalue '{surfaceCvalue} J/(m2*K)'." + "\n");
                };

                //Get the EnergyAnalyticalSpace of the surface and the physical space.
                EnergyAnalysisSpace space1 = energyAnalysisSurface.GetAnalyticalSpace();
                if (space1 != null)
                {
                    string space1Guid = space1.UniqueId;
                    string space1ID = space1.Id.ToString();
                    string space1Name = space1.Name;
                    double space1Area = Math.Round(UnitUtils.ConvertFromInternalUnits(space1.Area, UnitTypeId.SquareMeters), 2, MidpointRounding.ToEven);
                    double space1Volume = Math.Round(UnitUtils.ConvertFromInternalUnits(space1.InnerVolume, UnitTypeId.CubicMeters), 2, MidpointRounding.ToEven);
                    SpatialElement spatialElement1 = doc.GetElement(space1.CADObjectUniqueId) as SpatialElement;
                    string spatialElement1Guid = spatialElement1 == null ? ElementId.InvalidElementId.ToString() : spatialElement1.UniqueId;
                    //Get extra attributes such as area, volume, occupancy etc.
                    sb.Append(
                      $"inst:AE_Space_{space1Guid} a bot-ext:EnergyAnalyticalSpace ;" + "\n"
                    + $"\tprops:hasRevitId '{space1ID}'^^xsd:string ;" + "\n"
                    + $"\tprops:hasArea '{space1Area} m2' ;" + "\n"
                    + $"\tprops:hasVolume '{space1Volume} m3' ;" + "\n"
                    + $"\trdfs:label '{space1Name}, an energy analysis space'^^xsd:string ." + "\n"
                    + $"inst:Space_{spatialElement1Guid} bot:containsZone inst:AE_Space_{space1Guid}." + "\n"
                    + $"inst:Interface_{surfaceID}_1 bot:interfaceOf inst:AE_Space_{space1Guid}." + "\n"
                    );

                    if (spatialSurfaceElement != null)
                    {
                        sb.Append($"inst:Space_{spatialElement1Guid} bot:adjacentElement inst:Element_{spatialSurfaceElementId}." + "\n");
                    }

                    // Get the opening information related to the surface, and the opening attributes
                    /*
                     * Member name 	Description
                            Door 	Door.
                            Window 	Window.
                            SkylightSkylight.
                            Air 	Air. 
                     */
                    IList<EnergyAnalysisOpening> openings = energyAnalysisSurface.GetAnalyticalOpenings();
                    if (openings != null)
                    {
                        foreach (EnergyAnalysisOpening opening in openings)
                        {
                            string openingID = opening.UniqueId;
                            string openingType = opening.OpeningType.ToString();
                            double openingHeight = Math.Round(UnitUtils.ConvertFromInternalUnits(opening.Height, UnitTypeId.Meters), 2, MidpointRounding.ToEven);
                            double openingWidth = Math.Round(UnitUtils.ConvertFromInternalUnits(opening.Width, UnitTypeId.Meters), 2, MidpointRounding.ToEven);
                            double openingArea = Math.Round(openingHeight * openingWidth, 2, MidpointRounding.ToEven);

                            sb.Append(
                                     $"inst:Opening_{openingID}_1 a bot:Interface ;" + "\n"
                                   + $"\tprops:hasHeight '{openingHeight} m';" + "\n"
                                   + $"\tprops:hasWidth '{openingWidth} m';" + "\n"
                                   + $"\tprops:hasArea '{space1Area} m2';" + "\n"
                                   + $"\tprops:hasBoundary '{openingType}'^^xsd:string ;" + "\n"
                                   + $"\tprops:hasRevitId '{opening.Id.ToString()}'^^xsd:string ;" + "\n"
                                   + $"\tprops:relatesTo inst:Interface_{surfaceID}_1 ;" + "\n"
                                   + $"\tbot:interfaceOf inst:AE_Space_{space1Guid}." + "\n\n"
                                   );

                            Element AEopening = opening as Element;
                            if (AEopening != null)
                            {
                                if (AEopening.LookupParameter("HeatTransferCoefficient (U)") != null && uSwitch)
                                {
                                    double openingUvalue = Math.Round(UnitUtils.ConvertFromInternalUnits(AEopening.LookupParameter("HeatTransferCoefficient (U)").AsDouble(), UnitTypeId.WattsPerSquareMeterKelvin), 2, MidpointRounding.ToEven);
                                    sb.Append($"inst:Opening_{openingID}_1 props:hasUvalue '{openingUvalue} W/(m2*K)'." + "\n");
                                };

                                if (AEopening.LookupParameter("Thermal Mass") != null && cSwitch)
                                {
                                    double openingCvalue = Math.Round(UnitUtils.ConvertFromInternalUnits(opening.LookupParameter("Thermal Mass").AsDouble(), UnitTypeId.JoulesPerSquareMeterKelvin));
                                    sb.Append($"inst:Opening_{openingID}_1 props:hasCvalue '{openingCvalue}  J/(m2*K)'." + "\n");
                                };
                            }

                            Element spatialOpeningElement = doc.GetElement(opening.CADObjectUniqueId) as Element;
                            if (spatialOpeningElement != null)
                            {
                                string spatialOpeningElementId = spatialOpeningElement == null ? ElementId.InvalidElementId.ToString() : spatialOpeningElement.UniqueId;
                                sb.Append(
                                         $"inst:Element_{spatialOpeningElementId} props:hasRevitId '{spatialOpeningElement.Id.ToString()}'^^xsd:string." + "\n"
                                       + $"inst:Opening_{openingID}_1 bot:interfaceOf   inst:Element_{spatialOpeningElementId}." + "\n"
                                       + $"inst:Element_{spatialOpeningElementId} props:relatesTo inst:Element_{spatialSurfaceElementId}." + "\n"
                                );

                                sb.Append($"inst:Space_{spatialElement1Guid} bot:adjacentElement inst:Element_{spatialOpeningElementId}." + "\n");

                                if (openingType == "Door")
                                {
                                    sb.Append(
                                            $"inst:Element_{spatialOpeningElementId} a beo:Door ." + "\n");
                                }
                                else if (openingType == "Window")
                                {
                                    sb.Append(
                                            $"inst:Element_{spatialOpeningElementId} a beo:Window ." + "\n");
                                }
                            }

                        }

                    }

                    //Get the adjacent EnergyAnalyticalSpace of the space & the physical space.
                    EnergyAnalysisSpace space2 = energyAnalysisSurface.GetAdjacentAnalyticalSpace();
                    if (space2 != null && space1.UniqueId != space2.UniqueId)
                    {
                        string space2Guid = space2.UniqueId;
                        string space2ID = space2.Id.ToString();
                        string space2Name = space2.Name;
                        SpatialElement spatialElement2 = doc.GetElement(space2.CADObjectUniqueId) as SpatialElement;
                        string spatialElement2Guid = spatialElement2 == null ? ElementId.InvalidElementId.ToString() : spatialElement2.UniqueId;
                        double space2Area = Math.Round(UnitUtils.ConvertFromInternalUnits(space2.Area, UnitTypeId.SquareMeters), 2, MidpointRounding.ToEven);
                        double space2Volume = Math.Round(UnitUtils.ConvertFromInternalUnits(space2.InnerVolume, UnitTypeId.CubicMeters), 2, MidpointRounding.ToEven);

                        sb.Append(
                              $"inst:AE_Space_{space1Guid} bot:adjacentZone inst:AE_Space_{space2Guid}." + "\n"
                            + $"inst:Interface_{surfaceID}_2 bot:interfaceOf inst:AE_Space_{space2Guid};" + "\n"
                            + $"\tbot:interfaceOf inst:Element_{spatialSurfaceElementId}." + "\n"
                            + $"inst:Interface_{surfaceID}_2 bot-ext:inverseOf inst:Interface_{surfaceID}_1 ." + "\n"   
                            + $"inst:AE_Space_{space2Guid} a bot-ext:EnergyAnalyticalSpace ;" + "\n"
                            + $"\tprops:hasRevitId '{space2ID}'^^xsd:string ;" + "\n"
                            + $"\tprops:hasArea '{space2Area} m2' ;" + "\n"
                            + $"\tprops:hasVolume '{space2Volume} m3' ;" + "\n"
                            + $"\trdfs:label '{space2Name}, an energy analysis space'^^xsd:string ." + "\n"
                            + $"inst:Space_{spatialElement2Guid} bot:containsZone inst:AE_Space_{space2Guid} ;" + "\n"
                            + $"\tbot:adjacentZone inst:Space_{spatialElement1Guid} ." + "\n"
                            );

                        sb.Append(
                                  $"inst:Interface_{surfaceID}_2 a bot:Interface;" + "\n"
                                + $"\tprops:hasBoundary '{surfaceType}'^^xsd:string;" + "\n"
                                + $"\tprops:hasHeight '{surfaceHeight} m';" + "\n"
                                + $"\tprops:hasWidth '{surfaceWidth} m';" + "\n"
                                + $"\tprops:hasArea '{surfaceArea} m2';" + "\n"
                                + $"\tprops:hasOrientation '{opposite_orientation}'^^xsd:string;" + "\n"
                                + $"\tprops:hasTilt '{surfaceTilt} rad';" + "\n"
                                + $"\tprops:hasRevitId '{surface.Id.ToString()}'^^xsd:string ." + "\n"
                                );

                        if (spatialSurfaceElement != null)
                        {
                            sb.Append($"inst:Space_{spatialElement2Guid} bot:adjacentElement inst:Element_{spatialSurfaceElementId}." + "\n");
                        }

                        // Get the opening information related to the surface, and the opening attributes
                        /*
                         * Member name 	Description
                                Door 	Door.
                                Window 	Window.
                                SkylightSkylight.
                                Air 	Air. 
                         */
                        if (openings != null)
                        {
                            foreach (EnergyAnalysisOpening opening in openings)
                            {
                                string openingID = opening.UniqueId;
                                string openingType = opening.OpeningType.ToString();
                                double openingHeight = Math.Round(UnitUtils.ConvertFromInternalUnits(opening.Height, UnitTypeId.Meters), 2, MidpointRounding.ToEven);
                                double openingWidth = Math.Round(UnitUtils.ConvertFromInternalUnits(opening.Width, UnitTypeId.Meters), 2, MidpointRounding.ToEven);
                                double openingArea = Math.Round(openingHeight * openingWidth, 2, MidpointRounding.ToEven);

                                sb.Append(
                                         $"inst:Opening_{openingID}_2 a bot:Interface ;" + "\n"
                                       + $"\tbot-ext:inverseOf inst:Opening_{openingID}_1;"
                                       + $"\tprops:hasHeight '{openingHeight} m';" + "\n"
                                       + $"\tprops:hasWidth '{openingWidth} m';" + "\n"
                                       + $"\tprops:hasArea '{space1Area} m2';" + "\n"
                                       + $"\tprops:hasBoundary '{openingType}'^^xsd:string ;" + "\n"
                                       + $"\tprops:hasRevitId '{opening.Id.ToString()}'^^xsd:string ;" + "\n"
                                       + $"\tprops:relatesTo inst:Interface_{surfaceID}_2 ;" + "\n"
                                       + $"\tbot:interfaceOf inst:AE_Space_{space2Guid}." + "\n\n"
                                       );

                                Element AEopening = opening as Element;
                                if (AEopening != null)
                                {
                                    if (AEopening.LookupParameter("HeatTransferCoefficient (U)") != null && uSwitch)
                                    {
                                        double openingUvalue = Math.Round(UnitUtils.ConvertFromInternalUnits(AEopening.LookupParameter("HeatTransferCoefficient (U)").AsDouble(), UnitTypeId.WattsPerSquareMeterKelvin), 2, MidpointRounding.ToEven);
                                        sb.Append($"inst:Opening_{openingID}_2 props:hasUvalue '{openingUvalue} W/(m2*K)'." + "\n");
                                    };

                                    if (AEopening.LookupParameter("Thermal Mass") != null && cSwitch)
                                    {
                                        double openingCvalue = Math.Round(UnitUtils.ConvertFromInternalUnits(opening.LookupParameter("Thermal Mass").AsDouble(), UnitTypeId.JoulesPerSquareMeterKelvin));
                                        sb.Append($"inst:Opening_{openingID}_2 props:hasCvalue '{openingCvalue} J/(m2*K)'." + "\n");
                                    };
                                }

                                Element spatialOpeningElement = doc.GetElement(opening.CADObjectUniqueId) as Element;
                                if (spatialOpeningElement != null)
                                {
                                    string spatialOpeningElementId = spatialOpeningElement == null ? ElementId.InvalidElementId.ToString() : spatialOpeningElement.UniqueId;
                                    sb.Append(
                                             $"inst:Opening_{openingID}_2 bot:interfaceOf  inst:Element_{spatialOpeningElementId}." + "\n"
                                    );

                                    sb.Append($"inst:Space_{spatialElement2Guid} bot:adjacentElement inst:Element_{spatialOpeningElementId}." + "\n");
                                }

                            }

                        }

                    }
                    else if (surfaceType == "Air" || surfaceType == "Roof")
                    {
                        sb.Append(
                             $"inst:AE_Space_{space1Guid} bot:adjacentZone inst:Ambience_Air." + "\n"
                            + $"inst:Interface_{surfaceID}_1 bot:interfaceOf inst:Ambience_Air ." + "\n"
                            );
                    }
                    else if (surfaceType == "ExteriorFloor" || surfaceType == "Underground")
                    {
                        sb.Append(
                              $"inst:AE_Space_{space1Guid} bot:adjacentZone inst:Ambience_Ground." + "\n"
                            + $"inst:Interface_{surfaceID}_1 bot:interfaceOf inst:Ambience_Ground ." + "\n"
                            );
                    }

                }
            
            }

            //IList<EnergyAnalysisSpace> spaces = eadm.GetAnalyticalSpaces();
            //foreach (EnergyAnalysisSpace space in spaces)
            //{
            //    string spaceName = space.Name;
            //    string spaceGuid = space.UniqueId;
            //    string spaceID = space.Id.ToString();
            //    SpatialElement spatialElement = doc.GetElement(space.CADObjectUniqueId) as SpatialElement;
            //    string spatialElementId = spatialElement == null ? ElementId.InvalidElementId.ToString() : spatialElement.Id.ToString();
            //    sb.Append(
            //      $"inst:AE_Space_{spaceGuid} a bot-ext:EnergyAnalyticalSpace ;" + "\n"
            //    + $"props:hasGuid '{spaceID}'^^xsd:string ;" + "\n"
            //    + $"rdfs:label '{spaceName}, an energy analysis space'^^xsd:string ." + "\n"
            //    + $"inst:Space_{spatialElementId} bot:containsZoneinst:AE_Space_{spaceGuid} ." + "\n"
            //    );

            //    IList<EnergyAnalysisSurface> surfacesTrial = space.GetAnalyticalSurfaces();
            //    if (surfacesTrial != null)
            //    {
            //        foreach (EnergyAnalysisSurface surface in surfacesTrial)
            //        {
            //            string surfaceID = surface.UniqueId;
            //            SpatialElement spatialSurfaceElement = doc.GetElement(surface.CADObjectUniqueId) as SpatialElement;
            //            string spatialSurfaceElementId = spatialSurfaceElement == null ? ElementId.InvalidElementId.ToString() : spatialSurfaceElement.Id.ToString();
            //            sb.Append(
            //               $"inst:AE_Sur_{surfaceID} bot:interfaceOf inst:AE_Space_{spaceGuid}." + "\n"
            //              + $"inst:Space_{spatialElementId} bot:adjacentElement inst:Sur_{spatialSurfaceElementId}." + "\n"
            //            );
            //            builder.AppendLine("            +++ Surface from " + surface.OriginatingElementDescription);
            //        }
            //    }
            //}

            // Create a filter to retrieve wall elements
            FilteredElementCollector wallcollector = new FilteredElementCollector(doc);
            wallcollector.OfClass(typeof(Wall));
            // Loop through each wall element
            foreach (Wall wall in wallcollector)
            {
                string wallID = wall.UniqueId;
                string wallRevitId = wall.Id.ToString();
                // Get the wall type
                ElementId wallTypeId = wall.GetTypeId();
                WallType wallType = doc.GetElement(wallTypeId) as WallType;
                string wallTypeFuncName = wallType.Function.ToString();
                double wallArea = Math.Round(UnitUtils.ConvertFromInternalUnits(wall.LookupParameter("Area").AsDouble(), UnitTypeId.SquareMeters), 2, MidpointRounding.ToEven);

                sb.Append(
                    $"inst:Element_{wallID} props:hasBoundary '{wallTypeFuncName}'^^xsd:String ;" + "\n"
                  + $"\tprops:hasArea '{wallArea} m2' ;" + "\n"
                  + $"\tprops:hasRevitId '{wallRevitId}'^^xsd:string ." + "\n"
                  );

            }

            TaskDialog.Show("EAM", "Get analytical geometry and described using interfaces, Done!");
            return sb;

        }

        public static string GetParameterValue(Parameter parameter)
        {
            switch (parameter.StorageType)
            {
                case StorageType.String:
                    return parameter.AsString();
                case StorageType.Integer:
                    return parameter.AsInteger().ToString();
                case StorageType.Double:
                    return parameter.AsDouble().ToString();
                case StorageType.ElementId:
                    return parameter.AsElementId().ToString();
                default:
                    return parameter.AsValueString();
            }
        }
    }
}

