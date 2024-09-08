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
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using System.Globalization;
using Result = Autodesk.Revit.UI.Result;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Query;
using VDS.RDF.Shacl;
using VDS.RDF.Shacl.Validation;
using System.Windows.Forms;
using Application = Autodesk.Revit.ApplicationServices.Application;
using Form = System.Windows.Forms.Form;
using Graph = VDS.RDF.Graph;
using VDS.RDF.Shacl.Validation;
using INode = VDS.RDF.INode;
using Document = Autodesk.Revit.DB.Document;
using Element = Autodesk.Revit.DB.Element;
using System.ComponentModel;
using static System.Net.Mime.MediaTypeNames;
using static Autodesk.Revit.DB.SpecTypeId;
//using AngleSharp.Dom;


namespace FSOParser
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class Command : IExternalCommand
    {
        // set database name
        string databaseLocation = "InfluxDB_V1";
        string buildingNumber = "RNG";
        string azure_twin_ID = null;
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Change the culture to use a dot as the decimal separator
            CultureInfo newCulture = new CultureInfo(CultureInfo.CurrentCulture.Name);
            newCulture.NumberFormat.NumberDecimalSeparator = ".";

            // Apply the new culture
            CultureInfo.CurrentCulture = newCulture;

            //Switches for code generation
            var interfaceAnalysis = true;
            var hvacAnalysis = true;
            var shaclAnalysis = false;

            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;
            string lang = doc.Application.Language.ToString();
            ////A task window to show everything works again
            TaskDialog.Show("Language", lang);

            //*************
            //Set vairiables for attributes default in ENGU
            var coolingload = "Design Cooling Load";
            var heatingload = "Design Heating Load";
            var supplyairdemand = "Actual Supply Airflow";
            var returnairdemand = "Actual Return Airflow";

            //Set the attributes according to the language tag
            if (lang == "Deutsch")
            {
                coolingload = "Design Cooling Load";
                heatingload = "Design Heating Load";
                supplyairdemand = "Actual Supply Airflow";
                returnairdemand = "Actual Return Airflow";
            }

            //*************
            //Get the building and assign it as buildingname. Working
            ProjectInfo projectinfo = doc.ProjectInformation;
            string buildingname = projectinfo.BuildingName;
            string buildingAddress = projectinfo.Address;
            string buildingGuid = projectinfo.UniqueId.ToString();
            string docPathName = doc.PathName;

            ProjectLocation projectLocation = doc.ActiveProjectLocation;
            SiteLocation site = projectLocation.GetSiteLocation();
            string placeName_complete = site.PlaceName;
            char[] delimiters = new char[] { ' ', ',' }; // Split by space and comma
            string placeName = placeName_complete.Split(delimiters, StringSplitOptions.RemoveEmptyEntries)[0];



            double longitude_rad = site.Longitude;
            double latitude_rad = site.Latitude;

            double longitude = longitude_rad * (180/Math.PI);
            double latitude = latitude_rad * (180/Math.PI);

            string fpath = docPathName.Replace(".rvt", ".ttl"); // User can specifz where the output ttl file should be. 
            string docName = doc.Title;

            //************
            // Generate the header
            StringBuilder sb = new StringBuilder();
            sb.Append(

                $"# baseURI: file:/{fpath}\n" +
                "# imports: https://w3id.org/bot# \n" +
                "# imports: http://bosch-cr-aes//hsbc/fso-extension\r\n" +
                "# imports: http://bosch-cr-aes//hsbc/ssn-extension\r\n" +

                "# imports: https://brickschema.org/schema/1.3/Brick\r\n" +
                "# imports: http://bosch-cr-aes//hsbc/bot-extension\n\n" +
                "@prefix owl: <http://www.w3.org/2002/07/owl#> ." + "\n" +
                "@prefix rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#> ." + "\n" +
                "@prefix xml: <http://www.w3.org/XML/1998/namespace> ." + "\n" +
                "@prefix xsd: <http://www.w3.org/2001/XMLSchema#> ." + "\n" +
                "@prefix rdfs: <http://www.w3.org/2000/01/rdf-schema#> ." + "\n" +
                "@prefix beo: <https://pi.pauwel.be/voc/buildingelement#>." + "\n" +
                "@prefix bot: <https://w3id.org/bot#> ." + "\n" +
                "@prefix bot-ext: <http://bosch-cr-aes//hsbc/bot-extension#>." + "\n" +
                "@prefix brick: <https://brickschema.org/schema/Brick#> ." + "\n" +
                "@prefix props: <https://w3id.org/props#> ." + "\n" +
                "@prefix fso: <https://w3id.org/fso#> ." + "\n" +
                "@prefix fpo: <https://w3id.org/fpo#> ." + "\n" +
                "@prefix ssn: <http://www.w3.org/ns/ssn/> ." + "\n" +
                "@prefix sosa: <http://www.w3.org/ns/sosa/> ." + "\n" +
                "@prefix td: <https://www.w3.org/2019/wot/td#> ." + "\n" +
                "@prefix unit: <http://qudt.org/vocab/unit/> ." + "\n" +
                "@prefix inst: <https://example.com/inst#> ." + "\n" +
                "@prefix ssn-ext: <http://bosch-cr-aes//hsbc/ssn-extension#> ." + "\n" +
                "@prefix fso-ext: <http://bosch-cr-aes//hsbc/fso-extension#> ." + "\n" +
                "@prefix tso: <https://w3id.org/tso#> ." + "\n" +
                "@prefix geo: <http://www.w3.org/2003/01/geo/wgs84_pos#> ." + "\n" +
                "@prefix ref: <https://brickschema.org/schema/Brick/ref#> ." + "\n\n"
                );
            // "@prefix ex: <https://example.com/ex#> ." + "\n");+

            //Generate the building
            sb.Append($"inst:Building_{buildingGuid} a bot:Building ;" + "\n" +
                $"\t props:hasGuid '{buildingGuid}'^^xsd:string  ;" + "\n" +
                $"\t rdfs:label '{buildingname}'^^xsd:string  ;" + "\n" +
                $"\t geo:location inst:{placeName} ." + "\n\n");

            //Generate location
            sb.Append($"inst:{placeName} a geo:Point ;" + "\n" +
                $"\t geo:longitude '{longitude}'^^xsd:double  ;" + "\n" +
                $"\t geo:latitude '{latitude}'^^xsd:double ." + "\n\n");


            //Get all level and the building it is related to. WOKRING 
            FilteredElementCollector levelCollector = new FilteredElementCollector(doc);
            ICollection<Element> levels = levelCollector.OfClass(typeof(Level)).ToElements();
            List<Level> levelList = new List<Level>();
            foreach (Level level in levelCollector)
            {
                Level w = level as Level;
                string levelName = level.Name.Replace(' ', '-');
                string guidNumber = level.UniqueId.ToString();
                sb.Append($"inst:Storey_{guidNumber} a bot:Storey ;" + "\n" +
                    $"\t props:hasGuid '{guidNumber}'^^xsd:string  ;" + "\n" +
                    $"\t rdfs:label '{levelName}'^^xsd:string ;" + "\n" +
                    $"\tprops:hasRevitId '{level.Id.ToString()}'^^xsd:string ." + "\n"+
                    $"inst:Building_{buildingGuid} bot:hasStorey inst:Storey_{guidNumber} ." + "\n\n");
            }

            // Create a filtered element collector for architectural rooms
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            collector.OfCategory(BuiltInCategory.OST_Rooms);
            // Filter for room elements only
            collector.WhereElementIsNotElementType();
            // Iterate over the collected elements
            foreach (Room room in collector)
            {
                // Process each architectural room
                if (room.LookupParameter("Area").AsDouble() > 0)
                {
                    string roomGuid = room.UniqueId.ToString();
                    string roomId = room.Id.ToString();
                    string isSpaceOf = room.Level.UniqueId;
                    double roomarea = Math.Round(UnitUtils.ConvertFromInternalUnits(room.LookupParameter("Area").AsDouble(), UnitTypeId.SquareMeters), 2, MidpointRounding.ToEven);
                    double roomVolume = Math.Round(UnitUtils.ConvertFromInternalUnits(room.LookupParameter("Volume").AsDouble(), UnitTypeId.CubicMeters), 2, MidpointRounding.ToEven);
                    sb.Append(
                        $"inst:Space_{roomGuid} a bot-ext:Room ;" + "\n" +
                        $"\tprops:hasRevitId '{roomId}'^^xsd:string  ;" + "\n" +
                        //$"\tprops:hasArea '{roomarea}'^^xsd:double  ;\n" +
                        $"\tprops:hasArea '{roomarea.ToString("0.##")} m2'  ;\n" +
                        $"\tprops:hasVolume '{roomVolume} m3'  .\n" +
                        $"inst:Storey_{isSpaceOf} bot:hasSpace inst:Space_{roomGuid}." + "\n\n");
                }

            }

            // Create a filtered element collector for MEP spaces
            FilteredElementCollector MEPcollector = new FilteredElementCollector(doc);
            MEPcollector.OfCategory(BuiltInCategory.OST_MEPSpaces);
            // Filter for space elements only
            MEPcollector.WhereElementIsNotElementType();
            // Iterate over the collected MEP spaces
            foreach (Space mepSpace in MEPcollector)
            {
                // Get the associated architectural room of the MEP space
                string mepSpaceGuid = mepSpace.UniqueId;
                Room room = mepSpace.Room;
                if (room != null)
                {
                    // Access the properties of the architectural room
                    // For example, you can retrieve the room name, number, and other relevant information
                    string roomGuid = room.UniqueId;
                    string roomName = room.Name.Replace(' ', '_');
                    sb.Append(
                        $"inst:Space_{mepSpaceGuid} bot:containsZone inst:Space_{roomGuid} ;" + "\n" +
                        $"\tprops:hasRevitId '{mepSpace.Id.ToString()}'^^xsd:string ." + "\n"+
                        $"inst:Space_{roomGuid}   rdfs:label '{roomName} corresponds to the MEP space {mepSpace.Name} '^^xsd:string ." + "\n"
                        );
                }
            }

            // Get all MEP spaces and the attributes related to. WOKRING
            FilteredElementCollector roomCollector = new FilteredElementCollector(doc);
            ICollection<Element> rooms = roomCollector.OfClass(typeof(SpatialElement)).ToElements();
            List<SpatialElement> roomList = new List<SpatialElement>();
            foreach (SpatialElement space in roomCollector)
            {
                SpatialElement w = space as SpatialElement;
                if (space.Category.Name == "Spaces" & space.LookupParameter("Area").AsDouble() > 0)
                {
                    string spaceName = space.Name.Replace(' ', '_');
                    string spaceGuid = space.UniqueId.ToString();
                    string isSpaceOf = space.Level.UniqueId;
                    double area = Math.Round(UnitUtils.ConvertFromInternalUnits(space.LookupParameter("Area").AsDouble(), UnitTypeId.SquareMeters), 2, MidpointRounding.ToEven);
                    double spaceVolume = Math.Round(UnitUtils.ConvertFromInternalUnits(space.LookupParameter("Volume").AsDouble(), UnitTypeId.CubicMeters), 2, MidpointRounding.ToEven);

                    string designCoolingLoadID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                    double designCoolingLoad = Math.Round(UnitUtils.ConvertFromInternalUnits(space.LookupParameter(coolingload).AsDouble(), UnitTypeId.Watts), 2, MidpointRounding.ToEven);

                    string designHeatingLoadID = System.Guid.NewGuid().ToString().Replace(' ', '-');

                    double designHeatingLoad = Math.Round(UnitUtils.ConvertFromInternalUnits(space.LookupParameter(heatingload).AsDouble(), UnitTypeId.Watts), 2, MidpointRounding.ToEven);

                    string designSupplyAirflowID = System.Guid.NewGuid().ToString().Replace(' ', '-');

                    double designSupplyAirflow = Math.Round(UnitUtils.ConvertFromInternalUnits(space.LookupParameter(supplyairdemand).AsDouble(), UnitTypeId.LitersPerSecond), 2, MidpointRounding.ToEven);

                    string designReturnAirflowID = System.Guid.NewGuid().ToString().Replace(' ', '-');

                    double designReturnAirflow = Math.Round(UnitUtils.ConvertFromInternalUnits(space.LookupParameter(returnairdemand).AsDouble(), UnitTypeId.LitersPerSecond), 2, MidpointRounding.ToEven);

				sb.Append(
                        $"inst:Space_{spaceGuid} a bot-ext:HVACSpace ;" + "\n" +
                        $"\tssn:hasProperty inst:CoolingLoad_{designCoolingLoadID},inst:HeatingLoad_{designHeatingLoadID}, inst:Airflow_{designSupplyAirflowID}, inst:Airflow_{designReturnAirflowID} ;" + "\n" +
                        $"\tprops:hasGuid '{spaceGuid}'^^xsd:string  ;" + "\n" +
                        $"\tprops:hasArea '{area} m2'  ;\r\n" +
                        $"\tprops:hasVolume '{spaceVolume} m3'  ;\n" +
                        $"\trdfs:label '{spaceName}'^^xsd:string ." + "\n" +
                        $"inst:Storey_{isSpaceOf} bot:hasSpace inst:Space_{spaceGuid} ." + "\n" +
                            $"#Cooling Demand in Space_{spaceName}" + "\n" +
                            $"inst:CoolingLoad_{designCoolingLoadID} a ssn-ext:DesignCoolingDemand ;" + "\n" +
                            $"\tbrick:value '{designCoolingLoad}'^^xsd:double ;" + "\n" +
                            $"\tbrick:hasUnit unit:W ." + "\n" +

                            $"#Heating Demand in Space_{spaceName}" + "\n" +
                            $"inst:HeatingLoad_{designHeatingLoadID} a ssn-ext:DesignHeatingDemand ;" + "\n" +
                            $"\tbrick:value '{designHeatingLoad}'^^xsd:double ;" + "\n" +
                            $"\tbrick:hasUnit unit:W ." + "\n" +

                            $"#Supply Air Flow Demand in Space_{spaceName}" + "\n" +
                            $"inst:Airflow_{designSupplyAirflowID} a ssn-ext:DesignSupplyAirflowDemand ;" + "\n" +
                            $"\tbrick:value '{designSupplyAirflow}'^^xsd:double ;" + "\n" +
                            $"\tbrick:hasUnit unit:L-PER-SEC ." + "\n" +

                            $"#Return Air Flow Demand in Space_{spaceName}" + "\n" +
                            $"inst:Airflow_{designReturnAirflowID} a ssn-ext:DesignReturnAirflowDemand ;" + "\n" +
                            $"\tbrick:value '{designReturnAirflow}'^^xsd:double ;" + "\n" +
                            $"\tbrick:hasUnit unit:L-PER-SEC ." + "\n\n");
                };
            }

            // Model the geometric interface as BOT does
            // Get all geometrical building information for further thermal analysis
            if (interfaceAnalysis)
            {
                Interface.DetailedGeometry(doc, sb);
            }

            //Do HVAC system analysis
            if (hvacAnalysis)
            {

                //Relationship between ventilation systems and their components.
                FilteredElementCollector ventilationSystemCollector = new FilteredElementCollector(doc);
                ICollection<Element> ventilationSystems = ventilationSystemCollector.OfClass(typeof(MechanicalSystem)).ToElements();
                List<MechanicalSystem> ventilationSystemList = new List<MechanicalSystem>();
                foreach (MechanicalSystem system in ventilationSystemCollector)
                {
                    //Get systems
                    DuctSystemType systemType = system.SystemType;

                    string systemID = system.UniqueId;
                    string systemName = system.Name;
                    ElementId superSystemType = system.GetTypeId();
                    string fluidID = System.Guid.NewGuid().ToString().Replace(' ', '-');

                    switch (systemType)
                    {
                        case DuctSystemType.SupplyAir:
                            sb.Append($"inst:VentilationSys_{systemID} a fso:SupplySystem ;" + "\n" +
                                $"\trdfs:label '{systemName}'^^xsd:string ;" + "\n" +
                                $"\tprops:hasRevitId '{system.Id.ToString()}'^^xsd:string ;" + "\n" +
                                $"\tbrick:hasSubstance inst:Substance_{fluidID} ." + "\n" +
                                $"inst:Substance_{fluidID} a brick:Supply_Air ;" + "\n" +
                                $"\t rdfs:label 'Air'^^xsd:string .\n\n"

                                );
                            break;
                        case DuctSystemType.ReturnAir:
                            sb.Append($"inst:VentilationSys_{systemID} a fso:ReturnSystem ;" + "\n" +
                                      $"\trdfs:label '{systemName}'^^xsd:string ;" + "\n" +
                                      $"\tprops:hasRevitId '{system.Id.ToString()}'^^xsd:string ;" + "\n" +
                                $"\tbrick:hasSubstance inst:Substance_{fluidID} ." + "\n" +
                                $"inst:Substance_{fluidID} a brick:Return_Air ;" + "\n" +
                                $"\trdfs:label 'Air'^^xsd:string .\n\n"
                                );
                            break;
                        case
                        DuctSystemType.ExhaustAir:
                            sb.Append($"inst:VentilationSys_{systemID} a fso:ReturnSystem ;" + "\n" +
                                        $"\tprops:hasRevitId '{system.Id.ToString()}'^^xsd:string ;" + "\n" +
                                        $"\trdfs:label '{systemName}'^^xsd:string ;" + "\n" +
                                        $"\tbrick:hasSubstance inst:Substance_{fluidID} ." + "\n" +

                                        $"inst:Substance_{fluidID} a brick:Exhaust_Air ;" + "\n" +
                                        $"\trdfs:label 'Air'^^xsd:string .\n\n"
                                );
                            break;
                        default:
                            break;
                    }

                    ElementSet systemComponents = system.DuctNetwork;

                    // add system type (temporarily)
                    //string specificSystemType = doc.GetElement(superSystemType).LookupParameter("Name").AsValueString();
                    ElementType superSystemTypeElement = doc.GetElement(superSystemType) as ElementType;

                    if (superSystemTypeElement.LookupParameter("IfcExportAs") != null)
                    {

                        Parameter systemTest = (superSystemTypeElement.LookupParameter("IfcExportAs"));
                        string systemTestValue = systemTest.AsString();

                        if (systemTestValue == "CoolingSystem")
                        {
                            sb.Append(
                                $"inst:VentilationSys_{systemID} a tso:CoolingSystem .\n"
                            );
                        }

                        else if (systemTestValue == "HeatingSystem")
                        {
                            sb.Append(
                                $"inst:VentilationSys_{systemID} a tso:HeatingSystem .\n"
                            );
                        }

                        else if (systemTestValue == "VentilationSystem")
                        {
                            sb.Append(
                                $"inst:VentilationSys_{systemID} a tso:VentilationSystem .\n"
                            );
                        }

                    }

                    //Relate components to systems
                    foreach (Element component in systemComponents)
                    {
                        string componentID = component.UniqueId;
                        sb.Append($"inst:VentilationSys_{systemID} fso:hasComponent inst:Comp_{componentID} ." + "\n");
                    }
                }


                // *****************
                // Relationship between heating and cooling systems and their components. Working
                FilteredElementCollector hydraulicSystemCollector = new FilteredElementCollector(doc);
                ICollection<Element> hydraulicSystems = hydraulicSystemCollector.OfClass(typeof(PipingSystem)).ToElements();
                List<PipingSystem> hydraulicSystemList = new List<PipingSystem>();
                foreach (PipingSystem system in hydraulicSystemCollector)
                {
                    //Get systems
                    PipeSystemType systemType = system.SystemType;
                    string systemID = system.UniqueId;
                    string systemName = system.Name;
                    ElementId superSystemType = system.GetTypeId();

                    //Fluid
                    string fluidID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                    string fluidTemperatureID = System.Guid.NewGuid().ToString().Replace(' ', '-');

                    if (doc.GetElement(superSystemType).LookupParameter("Fluid Type") != null)
                    {
                        string flowType = doc.GetElement(superSystemType).LookupParameter("Fluid Type").AsValueString();
                        double fluidTemperature = Math.Round(UnitUtils.ConvertFromInternalUnits(doc.GetElement(superSystemType).LookupParameter("Fluid Temperature").AsDouble(), UnitTypeId.Celsius), 2, MidpointRounding.ToEven); //  Why make the user defined-property "Fluid TemperatureX"?


                        switch (systemType)
                        {
                            case PipeSystemType.SupplyHydronic:
                                sb.Append(
                                    $"inst:HydraulicSys_{systemID} a fso:SupplySystem ;" + "\n" +
                                    $"\trdfs:label '{systemName}'^^xsd:string ;" + "\n" +
                                    $"\tprops:hasRevitId '{system.Id.ToString()}'^^xsd:string ;" + "\n" +
                                    $"\tbrick:hasSubstance inst:Substance_{fluidID} ." + "\n" +

                                    $"inst:Substance_{fluidID} a brick:Supply_Water ;" + "\n" +
                                    $"\trdfs:label '{flowType}'^^xsd:string ;" + "\n" +
                                    $"\tssn:hasProperty inst:Temperature_{fluidTemperatureID} ." + "\n" +

                                    $"inst:Temperature_{fluidTemperatureID} a ssn-ext:NominalTemperature ;" + "\n" +
                                    $"\tbrick:value '{fluidTemperature}'^^xsd:double ;" + "\n" +
                                    $"\tbrick:hasUnit unit:DEG_C ." + "\n\n"
                                    );
                                break;
                            case PipeSystemType.ReturnHydronic:
                                sb.Append(
                                    $"inst:HydraulicSys_{systemID} a fso:ReturnSystem ;" + "\n" +
                                    $"\trdfs:label '{systemName}'^^xsd:string ;" + "\n" +
                                    $"\tprops:hasRevitId '{system.Id.ToString()}'^^xsd:string ;" + "\n" +
                                    $"\tbrick:hasSubstance inst:Substance_{fluidID} ." + "\n" +

                                    $"inst:Substance_{fluidID} a brick:Return_Water ;" + "\n" +
                                    $"\trdfs:label '{flowType}'^^xsd:string ;" + "\n" +
                                    $"\tssn:hasProperty inst:Temperature_{fluidTemperatureID} ." + "\n" +

                                    $"inst:Temperature_{fluidTemperatureID} a ssn-ext:NominalTemperature ;" + "\n" +
                                    $"\tbrick:value '{fluidTemperature}'^^xsd:double ;" + "\n" +
                                    $"\tbrick:hasUnit unit:DEG_C ." + "\n\n"

                                    );
                                break;
                            default:
                                break;
                        }

                        ElementSet systemComponents = system.PipingNetwork;

                        // add system type (temporarily)
                        //string specificSystemType = doc.GetElement(superSystemType).LookupParameter("Name").AsValueString();
                        ElementType superSystemTypeElement = doc.GetElement(superSystemType) as ElementType;
                        if (superSystemTypeElement.LookupParameter("IfcExportAs") != null)
                        {

                            Parameter systemTest = (superSystemTypeElement.LookupParameter("IfcExportAs"));
                            string systemTestValue = systemTest.AsString();

                            if (systemTestValue == "CoolingSystem")
                            {
                                sb.Append(
                                    $"inst:HydraulicSys_{systemID} a tso:CoolingSystem .\n"
                                );
                            }

                            else if (systemTestValue == "HeatingSystem")
                            {
                                sb.Append(
                                    $"inst:HydraulicSys_{systemID} a tso:HeatingSystem .\n"
                                );
                            }

                            else if (systemTestValue == "VentilationSystem")
                            {
                                sb.Append(
                                    $"inst:HydraulicSys_{systemID} a tso:VentilationSystem .\n"
                                );
                            }

                        }


                        //Relate components to systems
                        foreach (Element component in systemComponents)
                        {
                            string componentID = component.UniqueId;
                            sb.Append($"inst:HydraulicSys_{systemID} fso:hasComponent inst:Comp_{componentID} ." + "\n");
                        }
                    }
                }

                ////*****************

                //Get IfcExportAs components, pipe fittings, duct fittings
                FilteredElementCollector componentCollector = new FilteredElementCollector(doc);
                ICollection<Element> components = componentCollector.OfClass(typeof(FamilyInstance)).ToElements();
                List<FamilyInstance> componentList = new List<FamilyInstance>();
                foreach (FamilyInstance component in componentCollector)
                {
                    string componentID = component.UniqueId.ToString();
                    string revitID = component.Id.ToString();


                    //Pipe fittings 
                    if (component.Category.Name == "Pipe Fittings")
                    {
                        //sb.Append($"inst:Comp_{componentID} props:hasGuid '{revitID}'^^xsd:string ." + "\n");
                        sb.Append($"inst:Comp_{componentID} props:hasRevitId '{revitID}'^^xsd:string ." + "\n");

                        string fittingType = ((MechanicalFitting)component.MEPModel).PartType.ToString();
                        if (fittingType.ToString() == "Tee")
                        {
                            //MaterialType
                            string materialTypeID = System.Guid.NewGuid().ToString().Replace(' ', '-'); ;
                            string materialTypeValue = component.Name;
                            sb.Append(
                               $"inst:Comp_{componentID} a fso-ext:Tee ;" + "\n"
                             + $"\tssn:hasProperty inst:MaterialType_{materialTypeID} ." + "\n"
                             + $"inst:MaterialType_{materialTypeID} a ssn-ext:MaterialType ;" + "\n"
                             + $"\tbrick:value '{materialTypeValue}'^^xsd:string ." + "\n"
                             );
                        }
                        else if (fittingType.ToString() == "Elbow")
                        {
                            //MaterialType
                            string materialTypeID = System.Guid.NewGuid().ToString().Replace(' ', '-'); ;
                            string materialTypeValue = component.Name;
                            sb.Append(
                               $"inst:Comp_{componentID} a fso-ext:Elbow;"
                             + $"\tssn:hasProperty inst:MaterialType_{materialTypeID} ." + "\n"
                             + $"inst:MaterialType_{materialTypeID} a ssn-ext:MaterialType ;" + "\n"
                             + $"\tbrick:value '{materialTypeValue}'^^xsd:string ." + "\n"
                             );
                        }
                        else if (fittingType.ToString() == "Transition")
                        {
                            //MaterialType
                            string materialTypeID = System.Guid.NewGuid().ToString().Replace(' ', '-'); ;
                            string materialTypeValue = component.Name;
                            sb.Append(
                               $"inst:Comp_{componentID} a fso-ext:Transition;"
                             + $"\tssn:hasProperty inst:MaterialType_{materialTypeID} ." + "\n"
                             + $"inst:MaterialType_{materialTypeID} a ssn-ext:MaterialType ;" + "\n"
                             + $"\tbrick:value '{materialTypeValue}'^^xsd:string ." + "\n"
                             );

                        }

                        else if (fittingType.ToString() == "Cap")
                        {
                            string materialTypeID = System.Guid.NewGuid().ToString().Replace(' ', '-'); ;
                            string materialTypeValue = component.Name;
                            sb.Append(

                               $"inst:Comp_{componentID} a fso-ext:Cap;"
                             + $"\tssn:hasProperty inst:MaterialType_{materialTypeID} ." + "\n"
                             + $"inst:MaterialType_{materialTypeID} a ssn-ext:MaterialType ;" + "\n"
                             + $"\tbrick:value '{materialTypeValue}'^^xsd:string ." + "\n"
                             );


                        }
                        else
                        {
                            sb.Append(
                                        $"inst:Comp_{componentID} a fso:Fitting." + "\n"
                                     );
                        }

                        RelatedPorts.GenericConnectors(component, revitID, componentID, sb);
                    }

                    //Duct fittings
                    else if (component.Category.Name == "Duct Fittings")
                    {
                        //sb.Append($"inst:Comp_{componentID} props:hasGuid '{revitID}'^^xsd:string ." + "\n");
                        sb.Append($"inst:Comp_{componentID} props:hasRevitId '{revitID}'^^xsd:string ." + "\n");

                        string fittingType = ((MechanicalFitting)component.MEPModel).PartType.ToString();
                        if (fittingType.ToString() == "Tee")
                        {
                            //MaterialType
                            string materialTypeID = System.Guid.NewGuid().ToString().Replace(' ', '-'); ;
                            string materialTypeValue = component.Name;
                            sb.Append(
                               $"inst:Comp_{componentID} a fso-ext:Tee ;" + "\n"
                             + $"\tssn:hasProperty inst:MaterialType_{materialTypeID} ." + "\n"
                             + $"inst:MaterialType_{materialTypeID} a ssn-ext:MaterialType ;" + "\n"
                             + $"\tbrick:value '{materialTypeValue}'^^xsd:string ." + "\n"
                            );
                        }

                        else if (fittingType.ToString() == "Elbow")
                        {

                            //MaterialType
                            string materialTypeID = System.Guid.NewGuid().ToString().Replace(' ', '-'); ;
                            string materialTypeValue = component.Name;
                            sb.Append(
                               $"inst:Comp_{componentID} a fso-ext:Elbow;"
                             + $"\tssn:hasProperty inst:MaterialType_{materialTypeID} ." + "\n"
                             + $"inst:MaterialType_{materialTypeID} a ssn-ext:MaterialType ;" + "\n"
                             + $"\tbrick:value '{materialTypeValue}'^^xsd:string ." + "\n"
                            );
                        }

                        else if (fittingType.ToString() == "Transition")
                        {
                            //MaterialType
                            string materialTypeID = System.Guid.NewGuid().ToString().Replace(' ', '-'); ;
                            string materialTypeValue = component.Name;
                            sb.Append(
                               $"inst:Comp_{componentID} a fso-ext:Transition;"
                             + $"\tssn:hasProperty inst:MaterialType_{materialTypeID} ." + "\n"
                             + $"inst:MaterialType_{materialTypeID} a ssn-ext:MaterialType ;" + "\n"
                             + $"\tbrick:value '{materialTypeValue}'^^xsd:string ." + "\n"
                             );

                        }

                        else if (fittingType.ToString() == "Cap")
                        {
                            string materialTypeID = System.Guid.NewGuid().ToString().Replace(' ', '-'); ;
                            string materialTypeValue = component.Name;
                            sb.Append(

                               $"inst:Comp_{componentID} a fso-ext:Cap;"
                             + $"\tssn:hasProperty inst:MaterialType_{materialTypeID} ." + "\n"
                             + $"inst:MaterialType_{materialTypeID} a ssn-ext:MaterialType ;" + "\n"
                             + $"\tbrick:value '{materialTypeValue}'^^xsd:string ." + "\n"
                             );


                        }

                        else if (fittingType.ToString() == "Pants")
                        {

                            string materialTypeID = System.Guid.NewGuid().ToString().Replace(' ', '-'); ;
                            string materialTypeValue = component.Name;
                            sb.Append(
                               $"inst:Comp_{componentID} a fso-ext:Pants;"
                             + $"\tssn:hasProperty inst:MaterialType_{materialTypeID} ." + "\n"
                             + $"inst:MaterialType_{materialTypeID} a ssn-ext:MaterialType ;" + "\n"
                             + $"\tbrick:value '{materialTypeValue}'^^xsd:string ." + "\n"
                             );


                        }

                        else if (fittingType.ToString() == "TapAdjustable")
                        {

                            string materialTypeID = System.Guid.NewGuid().ToString().Replace(' ', '-'); ;
                            string materialTypeValue = component.Name;
                            sb.Append(

                               $"inst:Comp_{componentID} a fso-ext:Tap;"
                             + $"\tssn:hasProperty inst:MaterialType_{materialTypeID} ." + "\n"
                             + $"inst:MaterialType_{materialTypeID} a ssn-ext:MaterialType ;" + "\n"
                             + $"\tbrick:value '{materialTypeValue}'^^xsd:string ." + "\n"
                             );


                        }

                        else
                        {

                            sb.Append(
                                        $"inst:Comp_{componentID} a fso:Fitting ." + "\n"
                                     );
                        }

                        RelatedPorts.GenericConnectors(component, revitID, componentID, sb);

                    }


                    else
                    {
                        //change: check IfcExportAs parameter once for both functions
                        //IfcExportAs classes
                        ParseComponent(component, sb, doc);

                        //Parse sensors
                        ParseSensor(component, sb, doc);
                    }

                }



                //************************
                //Get all pipes 
                FilteredElementCollector pipeCollector = new FilteredElementCollector(doc);
                ICollection<Element> pipes = pipeCollector.OfClass(typeof(Pipe)).ToElements();
                List<Pipe> pipeList = new List<Pipe>();
                foreach (Pipe component in pipeCollector)
                {
                    Pipe w = component as Pipe;

                    //Type
                    string componentID = component.UniqueId.ToString();
                    string revitID = component.Id.ToString();
                    sb.Append(
                        $"inst:Comp_{componentID} a fso-ext:Pipe ." + "\n" +
                        $"inst:Comp_{componentID} props:hasRevitId '{revitID}'^^xsd:string ." + "\n");

                    if (component.PipeType.Roughness != null)
                    {
                        //Roughness
                        string roughnessID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        double rougnessValue = component.PipeType.Roughness;
                        sb.Append(
                           $"inst:Comp_{componentID} ssn:hasProperty inst:Roughness_{roughnessID} ." + "\n"
                         + $"inst:Roughness_{roughnessID} a ssn-ext:NominalRoughness ;" + "\n"
                         + $"\tbrick:value '{rougnessValue}'^^xsd:double ;" + "\n" +
                         $"\tbrick:hasUnit unit:M ." + "\n"
                         );
                    }
                    if (component.LookupParameter("Length") != null)
                    {
                        //Length
                        string lengthID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        double lengthValue = Math.Round(UnitUtils.ConvertFromInternalUnits(component.LookupParameter("Length").AsDouble(), UnitTypeId.Meters), 2, MidpointRounding.ToEven);
                        sb.Append(
                           $"inst:Comp_{componentID} ssn:hasProperty inst:Length_{lengthID} ." + "\n"
                         + $"inst:Length_{lengthID} a ssn-ext:Length ;" + "\n"
                         + $"\tbrick:value '{lengthValue}'^^xsd:double ;" + "\n"
                         + $"\tbrick:hasUnit unit:M ." + "\n"
                        );
                    }
                    if (component.LookupParameter("name") != null)
                    {
                        //MaterialType
                        string materialTypeID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        string materialTypeValue = component.Name;
                        sb.Append(
                           $"inst:Comp_{componentID} ssn:hasProperty inst:Material_{materialTypeID} ." + "\n"
                         + $"inst:Material_{materialTypeID} a ssn-ext:MaterialType ;" + "\n"
                         + $"\tbrick:value '{materialTypeValue}'^^xsd:string ." + "\n"
                        );

                    }
                    RelatedPorts.GenericConnectors(component, revitID, componentID, sb);
                }


                ////************************
                //Get all ducts 
                FilteredElementCollector ductCollector = new FilteredElementCollector(doc);
                ICollection<Element> ducts = ductCollector.OfClass(typeof(Duct)).ToElements();
                List<Duct> ductList = new List<Duct>();
                foreach (Duct component in ductCollector)
                {
                    Duct w = component as Duct;

                    //Type
                    string componentID = component.UniqueId.ToString();
                    string revitID = component.Id.ToString();

                    sb.Append(
                        $"inst:Comp_{componentID} a fso-ext:Duct ." + "\n" +
                        $"inst:Comp_{componentID} props:hasRevitId '{revitID}'^^xsd:string ." + "\n");


                    if (component.DuctType.Roughness != null)
                    {
                        //Roughness
                        string roughnessID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        double rougnessValue = Math.Round(component.DuctType.Roughness, 2, MidpointRounding.ToEven);
                        sb.Append(
                           $"inst:Comp_{componentID} ssn:hasProperty inst:Roughness_{roughnessID} ." + "\n"
                         + $"inst:Roughness_{roughnessID} a ssn-ext:NominalRoughness ;" + "\n"
                         + $"\tbrick:value '{rougnessValue}'^^xsd:double ;" + "\n" +
                         $"\tbrick:hasUnit unit:M ." + "\n"
                         );
                    }

                    if (component.LookupParameter("Length") != null)
                    {
                        //Length
                        string lengthID = System.Guid.NewGuid().ToString().Replace(' ', '-'); ;
                        double lengthValue = Math.Round(UnitUtils.ConvertFromInternalUnits(component.LookupParameter("Length").AsDouble(), UnitTypeId.Meters), 2, MidpointRounding.ToEven);
                        sb.Append(
                           $"inst:Comp_{componentID} ssn:hasProperty inst:Length_{lengthID} ." + "\n"
                         + $"inst:Length_{lengthID} a ssn-ext:Length ;" + "\n"
                         + $"\tbrick:value '{lengthValue}'^^xsd:double ;" + "\n"
                         + $"\tbrick:hasUnit unit:M ." + "\n"
                         );
                    }

                    if (component.LookupParameter("Hydraulic Diameter") != null)
                    {
                        //Outside diameter
                        string outsideDiameterID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        double outsideDiameterValue = UnitUtils.ConvertFromInternalUnits(component.LookupParameter("Hydraulic Diameter").AsDouble(), UnitTypeId.Meters);
                        sb.Append(
                           $"inst:Comp_{componentID} ssn:hasProperty inst:HydraulicDiameter_{outsideDiameterID} ." + "\n"
                         + $"inst:HydraulicDiameter_{outsideDiameterID} a ssn-ext:HydraulicDiameter ;" + "\n"
                         + $"\tbrick:value '{outsideDiameterValue}'^^xsd:double ;" + "\n"
                         + $"\tbrick:hasUnit 'meter'^^xsd:string ." + "\n"
                         );
                    }


                    //MaterialType
                    string materialTypeID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                    string materialTypeValue = component.Name;
                    sb.Append(
                       $"inst:Comp_{componentID} ssn:hasProperty inst:Material_{materialTypeID} ." + "\n"
                     + $"inst:Material_{materialTypeID} a ssn-ext:MaterialType ;" + "\n"
                     + $"\tbrick:value '{materialTypeValue}'^^xsd:string ." + "\n"
                     );


                    if (component.LookupParameter("FrictionFactor") != null)
                    {
                        //frictionFactor 
                        string frictionFactorID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        double frictionFactorValue = Math.Round(component.LookupParameter("FrictionFactor").AsDouble(), 2, MidpointRounding.ToEven);
                        sb.Append(
                           $"inst:Comp_{componentID} ssn:hasProperty inst:FrictionFactor_{frictionFactorID} ." + "\n"
                         + $"inst:FrictionFactor_{frictionFactorID} a ssn-ext:FrictionFactor ;" + "\n"
                         + $"inst:FrictionFactor_{frictionFactorID} brick:value '{frictionFactorValue}'^^xsd:double ." + "\n"
                         );
                    }

                    if (component.LookupParameter("Friction") != null)
                    {
                        //friction
                        string frictionID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        double frictionIDValue = Math.Round(component.LookupParameter("Friction").AsDouble(), 2, MidpointRounding.ToEven);
                        sb.Append(
                           $"inst:Comp_{componentID} ssn:hasProperty inst:Friction_{frictionID} ." + "\n"
                         + $"inst:Friction_{frictionID} a ssn-ext:Friction ;" + "\n"
                         + $"\tbrick:value '{frictionIDValue}'^^xsd:double ;" + "\n"
                         + $"\tbrick:hasUnit unit:PA-PER-M ." + "\n"
                         );
                    }

                    RelatedPorts.GenericConnectors(component, revitID, componentID, sb);
                }

                FilteredElementCollector flexDuctCollector = new FilteredElementCollector(doc);
                ICollection<Element> flexibleDucts = flexDuctCollector.OfClass(typeof(FlexDuct)).ToElements();
                List<FlexDuct> flexibleDuctList = new List<FlexDuct>();
                foreach (FlexDuct component in flexDuctCollector)
                {
                    FlexDuct w = component as FlexDuct;

                    //Type
                    string componentID = component.UniqueId.ToString();
                    string revitID = component.Id.ToString();

                    sb.Append(
                        $"inst:Comp_{componentID} a fso-ext:FlexDuct ." + "\n" +
                        $"inst:Comp_{componentID} props:hasRevitId '{revitID}'^^xsd:string ." + "\n");


                    if (component.FlexDuctType.Roughness != null)
                    {
                        //Roughness
                        string roughnessID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        double rougnessValue = Math.Round(component.FlexDuctType.Roughness, 2, MidpointRounding.ToEven);
                        sb.Append(
                           $"inst:Comp_{componentID} ssn:hasProperty inst:Roughness_{roughnessID} ." + "\n"
                         + $"inst:Roughness_{roughnessID} a ssn-ext:NominalRoughness ;" + "\n"
                         + $"\tbrick:value '{rougnessValue}'^^xsd:double ;" + "\n" +
                         $"\tbrick:hasUnit unit:M ." + "\n"
                         );
                    }

                    if (component.LookupParameter("Length") != null)
                    {
                        //Length
                        string lengthID = System.Guid.NewGuid().ToString().Replace(' ', '-'); ;
                        double lengthValue = Math.Round(UnitUtils.ConvertFromInternalUnits(component.LookupParameter("Length").AsDouble(), UnitTypeId.Meters), 2, MidpointRounding.ToEven);
                        sb.Append(
                           $"inst:Comp_{componentID} ssn:hasProperty inst:Length_{lengthID} ." + "\n"
                         + $"inst:Length_{lengthID} a ssn-ext:Length ;" + "\n"
                         + $"\tbrick:value '{lengthValue}'^^xsd:double ;" + "\n"
                         + $"\tbrick:hasUnit unit:M ." + "\n"
                         );
                    }

                    if (component.LookupParameter("Hydraulic Diameter") != null)
                    {
                        //Outside diameter
                        string outsideDiameterID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        double outsideDiameterValue = Math.Round(UnitUtils.ConvertFromInternalUnits(component.LookupParameter("Hydraulic Diameter").AsDouble(), UnitTypeId.Meters), 2, MidpointRounding.ToEven);
                        sb.Append(
                           $"inst:Comp_{componentID} ssn:hasProperty inst:HydraulicDiameter_{outsideDiameterID} ." + "\n"
                         + $"inst:HydraulicDiameter_{outsideDiameterID} a ssn-ext:HydraulicDiameter ;" + "\n"
                         + $"\tbrick:value '{outsideDiameterValue}'^^xsd:double ;" + "\n"
                         + $"\tbrick:hasUnit 'meter'^^xsd:string ." + "\n"
                         );
                    }


                    //MaterialType
                    string materialTypeID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                    string materialTypeValue = component.Name;
                    sb.Append(
                       $"inst:Comp_{componentID} ssn:hasProperty inst:Material_{materialTypeID} ." + "\n"
                     + $"inst:Material_{materialTypeID} a ssn-ext:MaterialType ;" + "\n"
                     + $"\tbrick:value '{materialTypeValue}'^^xsd:string ." + "\n"
                     );


                    if (component.LookupParameter("FrictionFactor") != null)
                    {
                        //frictionFactor 
                        string frictionFactorID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        double frictionFactorValue = Math.Round(component.LookupParameter("FrictionFactor").AsDouble(), 2, MidpointRounding.ToEven);
                        sb.Append(
                           $"inst:Comp_{componentID} ssn:hasProperty inst:FrictionFactor_{frictionFactorID} ." + "\n"
                         + $"inst:FrictionFactor_{frictionFactorID} a ssn-ext:FrictionFactor ;" + "\n"
                         + $"inst:FrictionFactor_{frictionFactorID} brick:value '{frictionFactorValue}'^^xsd:double ." + "\n"
                         );
                    }

                    if (component.LookupParameter("Friction") != null)
                    {
                        //friction
                        string frictionID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        double frictionIDValue = Math.Round(component.LookupParameter("Friction").AsDouble(), 2, MidpointRounding.ToEven);
                        sb.Append(
                           $"inst:Comp_{componentID} ssn:hasProperty inst:Friction_{frictionID} ." + "\n"
                         + $"inst:Friction_{frictionID} a ssn-ext:Friction ;" + "\n"
                         + $"\tbrick:value '{frictionIDValue}'^^xsd:double ;" + "\n"
                         + $"\tbrick:hasUnit unit:PA-PER-M ." + "\n"
                         );
                    }

                    RelatedPorts.GenericConnectors(component, revitID, componentID, sb);
                }

            }

            //Converting to string before post request
            string reader = sb.ToString();
            //Serialize the content into a text file
            System.IO.File.WriteAllText(fpath, reader);
            //Send the data to database
            var res = HttpClientHelper.POSTDataAsync(reader);
            ////A task window to show everything works again
            //TaskDialog.Show("Revit", reader.ToString());

            //Postman testing

            if (shaclAnalysis)
            {
                // User needs to change the path to the location that the shape graphs are located.
                string fpathShapes = "./../../../../../Ontology/Shapes/Ventilation/fso-ext_shapes_ventilation-connections.ttl";
 
                IGraph dataGraph = null;
                IGraph shapesGraph = null;
                VDS.RDF.Shacl.ShapesGraph validationGraph;

                VDS.RDF.Shacl.Validation.Report validationReport = null;

                // Define the namespace mappings
                NamespaceMapper mapper = new NamespaceMapper();
                mapper.AddNamespace("props", new Uri("https://w3id.org/props#"));
                mapper.AddNamespace("tso", new Uri("https://w3id.org/tso#"));

                //load data graph
                dataGraph = new Graph();
                dataGraph.NamespaceMap.Import(mapper);
                dataGraph.LoadFromFile(fpath, new TurtleParser());



                //load shape graph 
                // List of file paths where the Turtle files containing the SHACL shapes are located
                shapesGraph = new Graph();
                List<string> shapeFiles = new List<string>
            {
                //fpathShapes_1,
                fpathShapes
            };

                // Merge shapes into the graph
                foreach (string shapeFile in shapeFiles)
                {
                    Graph tempGraph = new Graph();
                    FileLoader.Load(tempGraph, shapeFile);
                    shapesGraph.Merge(tempGraph);
                }

                // Create a ShapesGraph from the SHACL shapes graph
                validationGraph = new ShapesGraph(shapesGraph);

                //load ontologies
                //OntologyGraph ontology = new OntologyGraph();
                //FileLoader.Load(ontology, "fpath_props");

                //merge graph

                Report report = validationGraph.Validate(dataGraph);

                int count = report.Results.Count;

                // Create a list to store the validation results
                List<(string FocusNode, string Message, int elementID)> validationResults = new List<(string FocusNode, string Message, int elementID)>();

                // Create a new form
                Form form = new Form()
                {
                    Text = "SHACL Violations",
                    Width = 600,
                    Height = 500
                };


                // Create a new ListBox
                ListBox listBox = new ListBox();
                listBox.Dock = DockStyle.Fill;
                form.Controls.Add(listBox);

                // Add the violating components to the ListBox
                foreach (VDS.RDF.Shacl.Validation.Result result in report.Results)
                {
                    // Get the focus node
                    string focusNode = result.FocusNode.ToString();

                    // Get the corresponding message
                    string m = result.Message.Value;

                    // get element
                    string uri = focusNode;
                    uri.Replace('#', ':');
                    //string sparqlQuery = $"SELECT ?value WHERE {{ <{uri}> props:hasGuid ?value }}";
                    string sparqlQuery = $@"
                PREFIX props: <https://w3id.org/props#>
                SELECT ?revitID
                WHERE {{
                    #<{uri}> props:hasGuid ?revitID ;
                    <{uri}> props:hasRevitId ?revitID ;
 
                }}";

                    SparqlResultSet elementSet = dataGraph.ExecuteQuery(sparqlQuery) as SparqlResultSet;
                    //SparqlResult Element = ElementSet.Results.First();
                    string ElementID_string = null;
                    int ElementID_int = 0;
                    foreach (SparqlResult element in elementSet)
                    {
                        INode node = element["revitID"];
                        if (node is VDS.RDF.LiteralNode literalNode)
                        {
                            string idString = literalNode.Value;
                            ElementID_string = idString;
                        }
                    }


                    if (ElementID_string != null)
                    {
                        ElementID_int = Int32.Parse(ElementID_string);

                    }

                    else
                    {
                        ElementID_int = 0;
                    }

                    if (ElementID_int != 0)
                    {
                        validationResults.Add((focusNode, m, ElementID_int));
                        listBox.Items.Add("Element: " + ElementID_int + ", Message: " + m);
                    }
                    else
                    {
                        validationResults.Add((focusNode, m, ElementID_int));
                        listBox.Items.Add("Element: no ID available" + ", Message: " + m);
                    }
                }

                // When a element is selected in the ListBox, select it in Revit
                listBox.SelectedIndexChanged += (sender, e) =>
                {

                    int index = listBox.SelectedIndex;
                    if (index >= 0 && index < validationResults.Count())
                    {
                        //ElementId elementId = elementSet[index].Id;
                        int id = validationResults[index].elementID;

                        if (id != 0)
                        {
                            ElementId elementId = new ElementId(id);

                            // Select the element in Revit
                            commandData.Application.ActiveUIDocument.Selection.SetElementIds(new List<ElementId>() { elementId });
                            uidoc.ShowElements(elementId);
                        }

                    }

                };

                // Show the form
                //form.ShowDialog();
                form.Show();
            }
            return Result.Succeeded;
        }

        public Result ParseComponent(FamilyInstance component, StringBuilder sb, Autodesk.Revit.DB.Document doc)
        {
            string componentID = component.UniqueId;
            string revitID = component.Id.ToString();


            if (component.Symbol.LookupParameter("IfcExportAs") != null)
            {
                //Type
                string componentType = component.Symbol.LookupParameter("IfcExportAs").AsString();
                //Fan
                if (component.Symbol.LookupParameter("IfcExportAs").AsString() == "IfcFan")
                {
                    //Type 
                    sb.Append($"inst:Comp_{componentID} a fso-ext:Fan ;" + "\n"
                        + $"\tprops:hasRevitId '{revitID}'^^xsd:string." + "\n");

                    if (component.LookupParameter("NominalPressureHead") != null)
                    {
                        //PressureRise
                        string pressureRiseID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        string pressureRiseValue = component.LookupParameter("NominalPressureHead").AsString();
                        sb.Append(
                           $"inst:Comp_{componentID} ssn:hasProperty inst:PressureRise_{pressureRiseID} ." + "\n"
                         + $"inst:PressureRise_{pressureRiseID} a ssn-ext:NominalPressureRise ;" + "\n"
                         + $"\tbrick:value  '{pressureRiseValue}'^^xsd:string ;" + "\n"
                         + $"\tbrick:hasUnit unit:PA ." + "\n"
                        );
                    }

                    if (component.LookupParameter("NominalMassflow") != null)
                    {
                        //Massflow in Kg/s
                        string massflowID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        string massflowValue = component.LookupParameter("NominalMassflow").AsString();
                        sb.Append($"inst:Comp_{componentID} ssn:hasProperty inst:Massflow_{massflowID} ." + "\n"
                         + $"inst:Massflow_{massflowID} a ssn-ext:NominalMassflow ;" + "\n"
                         + $"\tbrick:value  '{massflowValue}'^^xsd:string ;" + "\n"
                         + $"\tbrick:hasUnit  unit:KiloGM-PER-SEC ." + "\n");
                    }
 

                }
                //AHU as Fan (temporary)
                else if (component.Symbol.LookupParameter("IfcExportAs").AsString() == "IfcAHU")
                {
                    //Type 
                    sb.Append($"inst:Comp_{componentID} a fso-ext:AHU ;" + "\n"
                        + $"\tprops:hasRevitId '{revitID}'^^xsd:string." + "\n");

                    string componentID_supplyFan = System.Guid.NewGuid().ToString().Replace(' ', '-');
                    string componentID_returnFan = System.Guid.NewGuid().ToString().Replace(' ', '-');

                }
                //Pump
                else if (component.Symbol.LookupParameter("IfcExportAs").AsString() == "IfcPump")
                {
                    string hostname = null;
                    string hostGuid = null;
                    string hostGraphID = null;
                    string hostRevitID = null;

                    string measurementID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                    string measurementType = "FlowActuator";

                    string sensorID = System.Guid.NewGuid().ToString().Replace(' ', '-');

                    //string brickSensor = "Pump";
                    string measurementUnit = "0";
                    string timeseriesID = "0";
                    string timeseriesRandomID = System.Guid.NewGuid().ToString().Replace(' ', '-');

                    //Type 
                    sb.Append($"inst:Comp_{componentID} a fso-ext:Pump ;" + "\n"
                         + $"\tprops:hasRevitId '{revitID}'^^xsd:string ." + "\n");

                    if (component.LookupParameter("NominalPressureHead") != null)
                    {
                        //PressureRise
                        string pressureRiseID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        string pressureRiseValue = component.LookupParameter("NominalPressureHead").AsString();
                        sb.Append(
                           $"inst:Comp_{componentID} ssn:hasProperty inst:PressureRise_{pressureRiseID} ." + "\n"
                         + $"inst:PressureRise_{pressureRiseID} a ssn-ext:NominalPressureRise ;" + "\n"
                         + $"\tbrick:value  '{pressureRiseValue}'^^xsd:string ;" + "\n"
                         + $"\tbrick:hasUnit unit:PA ." + "\n"
                        );
                    }

                    if (component.LookupParameter("NominalMassflow") != null)
                    {
                        //Massflow in Kg/s
                        string massflowID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        string massflowValue = component.LookupParameter("NominalMassflow").AsString();
                        sb.Append($"inst:Comp_{componentID} ssn:hasProperty inst:Massflow_{massflowID} ." + "\n"
                         + $"inst:Massflow_{massflowID} a ssn-ext:NominalMassflow ;" + "\n"
                         + $"\tbrick:value  '{massflowValue}'^^xsd:string ;" + "\n"
                         + $"\tbrick:hasUnit  unit:KiloGM-PER-SEC ." + "\n");
                    }

                    if (component.LookupParameter("RB_AKS") != null)
                    {
                        if (component.LookupParameter("RB_AKS").AsString() != null)
                        {
                            timeseriesID = component.LookupParameter("RB_AKS").AsString();

                            sb.Append(
                                //make host a feature of interest
                                $"inst:Comp_{componentID} rdf:type sosa:FeatureOfInterest . \n" +
                                //connect feature of interest with measurement instance,
                                $"inst:Comp_{componentID} ssn:forProperty inst:{measurementType}_{measurementID} . \n\n"
                                );

                            //create measurement instance
                            sb.Append($"inst:{measurementType}_{measurementID} " + "\n" +
                                "\t" + "rdf:type sosa:ActuatableProperty " + " ; \n" +
                                "\t" + $"rdfs:label '{measurementType} {measurementID}' " + ";\n" +
                                ".\n\n"
                                );


                            //create sensor instance (How can we determine it is an actuator for sure ?)
                            //sb.Append($"inst:Actuator_{sensorID} " + "  \n" +
                            sb.Append($"inst:Actuator_{timeseriesID} " + "  \n" +
                                "\t" + "rdf:type sosa:Actuator " + " ; \n" +
                                "\t" + "rdf:type brick:Water_Flow_Sensor " + " ; \n" +
                                "\t" + $"rdfs:label 'virtual actuator' " + ";\n" +
                                "\t" + $"sosa:observes inst:{measurementType}_{measurementID} " + ";\n" +
                                //    "\t" + $"brick:hasUnit unit:{measurementUnit} " + ";\n" +
                                "\t" + $"ref:hasTimeseriesID '{timeseriesID}' " + ";\n" +
                                "\t" + $"ref:storedAt '{databaseLocation}' " + ";\n" +
                                ".\n\n"
                                );

                            //create timeseries reference instance
                            sb.Append($"inst:TimeRef_{timeseriesRandomID} " + "\n" +
                                "\t" + $"rdf:type ref:TimeseriesReference " + " ; \n" +
                                "\t" + $"ref:hasTimeseriesID '{timeseriesID}' " + " ; \n" +
                                "\t" + $"ref:storedAt '{databaseLocation}' " + ";\n" +
                                ".\n\n"
                                );

                            if (component.LookupParameter("Azure_Twin_ID") != null)
                            {

                                azure_twin_ID = component.LookupParameter("Azure_Twin_ID").AsString();

                                sb.Append($"inst:Actuator_{timeseriesID} " + "  \n" +
                                    "\t" + "rdf:type td:Thing " + " ; \n" +
                                    "\t" + $"td:name '{azure_twin_ID}' " + " ; \n" +
                                    "\t" + $"td:hasActionAffordance inst:ActionAffordance_{timeseriesRandomID}" + ". \n"
                                    );
                                //create td:ActionAffordance  instance
                                sb.Append($"inst:ActionAffordance_{timeseriesRandomID} a td:ActionAffordance." + "\n\n"
                                    );

                            }
                            else
                            {
                                azure_twin_ID = timeseriesID.Replace(buildingNumber, string.Empty);
                                sb.Append($"inst:Actuator_{timeseriesID} " + "  \n" +
                                           "\t" + "rdf:type td:Thing " + " ; \n" +
                                           "\t" + $"td:name '{azure_twin_ID}' " + " ; \n" +
                                           "\t" + $"td:hasActionAffordance inst:ActionAffordance_{timeseriesRandomID}" + ". \n"
                                          );
                                //create td:ActionAffordance  instance
                                sb.Append($"inst:ActionAffordance_{timeseriesRandomID} a td:ActionAffordance." + "\n\n"
                                    );
                            }
                        }
                    }

                }
                //Valve
                else if (component.Symbol.LookupParameter("IfcExportAs").AsString() == "IfcValve")
                {

                    string hostname = null;
                    string hostGuid = null;
                    string hostGraphID = null;
                    string hostRevitID = null;

                    string measurementID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                    string measurementType = "Flow";

                    string sensorID = System.Guid.NewGuid().ToString().Replace(' ', '-');

                    string brickSensor = "Flow_Sensor";
                    string measurementUnit = "0";
                    string timeseriesID = "0";
                    string timeseriesRandomID = System.Guid.NewGuid().ToString().Replace(' ', '-');

                    if (component.Symbol.LookupParameter("IfcExportType") != null)
                    {
                        string componentTypeEnum = component.Symbol.LookupParameter("IfcExportType").AsString();
                        if(componentTypeEnum == "ISOLATING")
                        {
                            //Type 
                            sb.Append($"inst:Comp_{componentID} a fso-ext:Valve ;" + "\n"
                                + $"\tprops:hasRevitId '{revitID}'^^xsd:string ." + "\n");
                        }

                        else if (componentTypeEnum == "REGULATING")
                        {
                            measurementType = "FlowController";
                            //Type
                            sb.Append($"inst:Comp_{componentID} a fso-ext:ControlledValve ;" + "\n"
                                + $"\tprops:hasRevitId '{revitID}'^^xsd:string ." + "\n");
                        }
                    }

                    else
                    {

                        //Type 
                        sb.Append($"inst:Comp_{componentID} a fso-ext:Valve ;" + "\n"
                            + $"\tprops:hasRevitId '{revitID}'^^xsd:string ." + "\n");
                    }

                    if (component.LookupParameter("NominalPressureDrop") != null)
                    {
                        //PressureDrop
                        string pressureDropID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        string pressureDropValue = component.LookupParameter("NominalPressureDrop").AsString();
                        sb.Append(
                            $"inst:Comp_{componentID} ssn:hasProperty inst:PressureDrop_{pressureDropID} ." + "\n"
                            + $"inst:PressureDrop_{pressureDropID} a ssn-ext:NominalPressureDrop ;" + "\n"
                            + $"\tbrick:value  '{pressureDropValue}'^^xsd:string ;" + "\n"
                            + $"\tbrick:hasUnit unit:PA ." + "\n"
                        );
                    }

                    if (component.LookupParameter("NominalMassflow") != null)
                    {
                        //Massflow in Kg/s
                        string massflowID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        string massflowValue = component.LookupParameter("NominalMassflow").AsString();
                        sb.Append($"inst:Comp_{componentID} ssn:hasProperty inst:Massflow_{massflowID} ." + "\n"
                            + $"inst:Massflow_{massflowID} a ssn-ext:NominalMassflow ;" + "\n"
                            + $"\tbrick:value  '{massflowValue}'^^xsd:string ;" + "\n"
                            + $"\tbrick:hasUnit  unit:KiloGM-PER-SEC ." + "\n");
                    }

                    if (component.LookupParameter("RB_AKS") != null)
                    {
                        if (component.LookupParameter("RB_AKS").AsString() != null)
                        {
                            timeseriesID = component.LookupParameter("RB_AKS").AsString();

                            sb.Append(
                                //make host a feature of interest
                                $"inst:Comp_{componentID} rdf:type sosa:FeatureOfInterest . \n" +
                                //connect feature of interest with measurement instance,
                                $"inst:Comp_{componentID} ssn:hasProperty inst:{measurementType}_{measurementID} . \n\n"
                            );

                            //create measurement instance
                            sb.Append($"inst:{measurementType}_{measurementID} " + "\n" +
                                "\t" + "rdf:type sosa:ObservableProperty " + " ; \n" +
                                "\t" + $"rdfs:label '{measurementType} {measurementID}' " + ";\n" +
                                ".\n\n"
                                );


                            //create sensor instance
                            //sb.Append($"inst:Sensor_{sensorID} " + "  \n" +
                            sb.Append($"inst:Sensor_{timeseriesID} " + "  \n" +
                                "\t" + "rdf:type sosa:Sensor " + " ; \n" +
                                "\t" + $"rdf:type brick:{brickSensor} " + " ; \n" +
                                "\t" + $"rdfs:label 'virtual sensor' " + ";\n" +
                                "\t" + $"sosa:observes inst:{measurementType}_{measurementID} " + ";\n" +
                                //    "\t" + $"brick:hasUnit unit:{measurementUnit} " + ";\n" +
                                "\t" + $"ref:hasTimeseriesID '{timeseriesID}' " + ";\n" +
                                "\t" + $"ref:storedAt '{databaseLocation}' " + ";\n" +
                                ".\n\n"
                                );

                            //create timeseries reference instance
                            sb.Append($"inst:TimeRef_{timeseriesRandomID} " + "\n" +
                                "\t" + $"rdf:type ref:TimeseriesReference " + " ; \n" +
                                "\t" + $"ref:hasTimeseriesID '{timeseriesID}' " + " ; \n" +
                                "\t" + $"ref:storedAt '{databaseLocation}' " + ";\n" +
                                ".\n\n"
                                );
                            if (component.LookupParameter("Azure_Twin_ID") != null)
                            {

                                azure_twin_ID = component.LookupParameter("Azure_Twin_ID").AsString();

                                sb.Append($"inst:Sensor_{timeseriesID} " + "  \n" +
                                    "\t" + "rdf:type td:Thing" + " ; \n" +
                                    "\t" + $"td:name '{azure_twin_ID}' " + " ; \n" +
                                    "\t" + $"td:hasPropertyAffordance inst:PropertyAffordance_{timeseriesRandomID}" + ". \n"
                                    );
                                //create td:ActionAffordance  instance
                                sb.Append($"inst:PropertyAffordance_{timeseriesRandomID} a td:PropertyAffordance." + "\n\n"
                                    );

                            }
                            else
                            {
                                azure_twin_ID = timeseriesID.Replace(buildingNumber, string.Empty);
                                sb.Append($"inst:Sensor_{timeseriesID} " + "  \n" +
                                           "\t" + "rdf:type td:Thing " + " ; \n" +
                                           "\t" + $"td:name '{azure_twin_ID}' " + " ; \n" +
                                           "\t" + $"td:hasPropertyAffordance inst:PropertyAffordance_{timeseriesRandomID}" + ". \n"
                                          );
                                //create td:ActionAffordance  instance
                                sb.Append($"inst:PropertyAffordance_{timeseriesRandomID} a td:PropertyAffordance." + "\n\n"
                                    );
                            }
                        }
                    }
                    

                }


                //Damper
                else if (component.Symbol.LookupParameter("IfcExportAs").AsString() == "IfcDamper")
                {
                    string hostname = null;
                    string hostGuid = null;
                    string hostGraphID = null;
                    string hostRevitID = null;

                    string measurementID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                    string measurementType = "Flow";

                    string sensorID = System.Guid.NewGuid().ToString().Replace(' ', '-');

                    string measurementUnit = "0";
                    string timeseriesID = "0";
                    string timeseriesRandomID = System.Guid.NewGuid().ToString().Replace(' ', '-');

                    if (component.Symbol.LookupParameter("IfcExportType") != null)
                    {
                        string componentTypeEnum = component.Symbol.LookupParameter("IfcExportType").AsString();
                        if (componentTypeEnum == "CONTROLDAMPER")
                        {
                            //Type 
                            sb.Append(
                                $"inst:Comp_{componentID} a fso-ext:Damper ;" + "\n"
                                + $"\tprops:hasRevitId '{revitID}'^^xsd:string ." + "\n"
                            );

                        }

                        else if (componentTypeEnum == "FIREDAMPER")
                        {
                            sb.Append(
                              $"inst:Comp_{componentID} a fso-ext:FireDamper ;" + "\n"
                              + $"\tprops:hasRevitId '{revitID}'^^xsd:string ." + "\n"
                            );

                        }
                    } else
                    {
                        //Type 
                        sb.Append(
                            $"inst:Comp_{componentID} a fso-ext:Damper ;" + "\n"
                            + $"\tprops:hasRevitId '{revitID}'^^xsd:string ." + "\n"
                        );

                    }


                    if (component.LookupParameter("NominalPressureDrop") != null)
                    {
                        //PressureDrop
                        string pressureDropID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        string pressureDropValue = component.LookupParameter("NominalPressureDrop").AsString();
                        sb.Append(
                           $"inst:Comp_{componentID} ssn:hasProperty inst:PressureDrop_{pressureDropID} ." + "\n"
                         + $"inst:PressureDrop_{pressureDropID} a ssn-ext:NominalPressureDrop ;" + "\n"
                         + $"\tbrick:value  '{pressureDropValue}'^^xsd:string ;" + "\n"
                         + $"\tbrick:hasUnit unit:PA ." + "\n"
                        );
                    }

                    if (component.LookupParameter("NominalMassflow") != null)
                    {
                        //Massflow in Kg/s
                        string massflowID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        string massflowValue = component.LookupParameter("NominalMassflow").AsString();
                        sb.Append($"inst:Comp_{componentID} ssn:hasProperty inst:Massflow_{massflowID} ." + "\n"
                         + $"inst:Massflow_{massflowID} a ssn-ext:NominalMassflow ;" + "\n"
                         + $"\tbrick:value  '{massflowValue}'^^xsd:string ;" + "\n"
                         + $"\tbrick:hasUnit  unit:KiloGM-PER-SEC ." + "\n");
                    }

                   

                    // Get the FamilySymbol (type) of the FamilyInstance
                    FamilySymbol familySymbol = component.Symbol;

                    // Assume 'parameterName' is the name of the parameter you want to get
                    Parameter parameter = familySymbol.LookupParameter("D");

                    if (parameter != null)
                    {
                        // Now you can get the value of the parameter
                        // The specific method to use depends on the type of the parameter
                        // Here's how you might get it if it's a string parameter
                        string value = parameter.AsString();

                        // If it's a double parameter, you would do this:
                        // double value = parameter.AsDouble();

                        // Do something with the value...

                        //Outside diameter
                        string outsideDiameterID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        double outsideDiameterValue = UnitUtils.ConvertFromInternalUnits(parameter.AsDouble(), UnitTypeId.Millimeters);
                        sb.Append(
                            $"inst:Comp_{componentID} ssn:hasProperty inst:HydraulicDiameter_{outsideDiameterID} ." + "\n"
                            + $"inst:HydraulicDiameter_{outsideDiameterID} a ssn-ext:HydraulicDiameter ;" + "\n"
                            + $"\tbrick:value '{outsideDiameterValue}'^^xsd:double ;" + "\n"
                            + $"\tbrick:hasUnit unit:MilliM ." + "\n"
                            );
                    }


                        
                    




                    if (component.LookupParameter("RB_AKS") != null)
                    {
                        if (component.LookupParameter("RB_AKS").AsString() != null)
                        {
                            timeseriesID = component.LookupParameter("RB_AKS").AsString();

                            sb.Append(
                                //make host a feature of interest
                                $"inst:Comp_{componentID} rdf:type sosa:FeatureOfInterest . \n" +
                                //connect feature of interest with measurement instance,
                                $"inst:Comp_{componentID} ssn:forProperty inst:{measurementType}_{measurementID} . \n\n"
                            );

                            //create measurement instance
                            sb.Append($"inst:{measurementType}_{measurementID} " + "\n" +
                                "\t" + "rdf:type sosa:ActuatableProperty " + " ; \n" +
                                "\t" + $"rdfs:label '{measurementType} {measurementID}' " + ";\n" +
                                ".\n\n"
                                );


                            //create sensor instance
                            //sb.Append($"inst:Actuator_{sensorID} " + "  \n" +
                            sb.Append($"inst:Actuator_{timeseriesID} " + "  \n" +
                                "\t" + "rdf:type sosa:Actuator " + " ; \n" +
                                // or angle sensor?
                                "\t" + "rdf:type brick:Air_Flow_Sensor " + " ; \n" +
                                "\t" + $"rdfs:label 'virtual actuator' " + ";\n" +
                                "\t" + $"sosa:observes inst:{measurementType}_{measurementID} " + ";\n" +
                                //    "\t" + $"brick:hasUnit unit:{measurementUnit} " + ";\n" +
                                "\t" + $"ref:hasTimeseriesID '{timeseriesID}' " + ";\n" +
                                "\t" + $"ref:storedAt '{databaseLocation}' " + ";\n" +
                                ".\n\n"
                                );

                            //create timeseries reference instance
                            sb.Append($"inst:TimeRef_{timeseriesRandomID} " + "\n" +
                                "\t" + $"rdf:type ref:TimeseriesReference " + " ; \n" +
                                "\t" + $"ref:hasTimeseriesID '{timeseriesID}' " + " ; \n" +
                                "\t" + $"ref:storedAt '{databaseLocation}' " + ";\n" +
                                ".\n\n"
                                );

                            if (component.LookupParameter("Azure_Twin_ID") != null)
                            {

                                azure_twin_ID = component.LookupParameter("Azure_Twin_ID").AsString();

                                sb.Append($"inst:Actuator_{timeseriesID} " + "  \n" +
                                    "\t" + "rdf:type td:Thing " + " ; \n" +
                                    "\t" + $"td:name '{azure_twin_ID}' " + " ; \n" +
                                    "\t" + $"td:hasActionAffordance inst:ActionAffordance_{timeseriesRandomID}" + ". \n"
                                    );
                                //create td:ActionAffordance  instance
                                sb.Append($"inst:ActionAffordance_{timeseriesRandomID} a td:ActionAffordance." + "\n\n"
                                    );

                            }
                            else
                            {
                                azure_twin_ID = timeseriesID.Replace(buildingNumber, string.Empty);
                                sb.Append($"inst:Actuator_{timeseriesID} " + "  \n" +
                                           "\t" + "rdf:type td:Thing " + " ; \n" +
                                           "\t" + $"td:name '{azure_twin_ID}' " + " ; \n" +
                                           "\t" + $"td:hasActionAffordance inst:ActionAffordance_{timeseriesRandomID}" + ". \n"
                                          );
                                //create td:ActionAffordance  instance
                                sb.Append($"inst:ActionAffordance_{timeseriesRandomID} a td:ActionAffordance." + "\n\n"
                                    );
                            }
                        }
                    }



                }

                //Radiator
                else if (component.Symbol.LookupParameter("IfcExportAs").AsString() == "IfcSpaceHeater")
                {
                    string hostname = null;
                    string hostGuid = null;
                    string hostGraphID = null;
                    string hostRevitID = null;

                    string measurementID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                    string measurementType = "Temp";

                    string sensorID = System.Guid.NewGuid().ToString().Replace(' ', '-');

                    string brickSensor = "Temperature_Sensor";
                    string measurementUnit = "0";
                    string timeseriesID = "0";
                    string timeseriesRandomID = System.Guid.NewGuid().ToString().Replace(' ', '-');


                    //Type
                    sb.Append($"inst:Comp_{componentID} a fso-ext:Radiator ;" + "\n"
                        + $"\tprops:hasRevitId '{revitID}'^^xsd:string ." + "\n");

                    //DesignHeatPower
                    if (component.Symbol.LookupParameter("NominalPower") != null)
                    {
                        string designHeatPowerID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        string designHeatPowerValue = component.Symbol.LookupParameter("NominalPower").AsString();
                        if (designHeatPowerValue != null)
                        {
                            sb.Append(
                               $"inst:Comp_{componentID} ssn:hasProperty inst:NominalHeatingPower_{designHeatPowerID} ." + "\n"
                             + $"inst:NominalHeatingPower_{designHeatPowerID} a ssn-ext:NominalPower ;" + "\n"
                             + $"\tbrick:value '{designHeatPowerValue}'^^xsd:double ;" + "\n"
                             + $"\tbrick:hasUnit  unit:W ." + "\n"
                             );
                        }
                    }

                    //design massflow
                    if (component.Symbol.LookupParameter("NominalMassflow") != null)
                    {
                        string designMassflowID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        double designMassflowValue = Math.Round(UnitUtils.ConvertFromInternalUnits(component.Symbol.LookupParameter("NominalMassflow").AsDouble(), UnitTypeId.LitersPerSecond), 2, MidpointRounding.ToEven);
                        sb.Append(
                           $"inst:Comp_{componentID} ssn:hasProperty inst:NominalMassflow_{designMassflowID} ." + "\n"
                         + $"inst:NominalMassflow_{designMassflowID} a ssn-ext:NominalMassflow ;" + "\n"
                         + $"\tbrick:value '{designMassflowValue}'^^xsd:double ;" + "\n"
                         + $"\tbrick:hasUnit  unit:Lps ." + "\n"
                         );
                    }

                    //Nominal supply watet temperature
                    if (component.Symbol.LookupParameter("NominalSupplyTemperature") != null)
                    {
                        string designSupTempID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        string designSupTempValue = component.Symbol.LookupParameter("NominalSupplyTemperature").AsString();
                        sb.Append(
                           $"inst:Comp_{componentID} ssn:hasProperty inst:NominalSupplyTemperature_{designSupTempID} ." + "\n"
                         + $"inst:NominalSupplyTemperature_{designSupTempID} a ssn-ext:NominalSupplyTemperature ;" + "\n"
                         + $"\tbrick:value '{designSupTempValue}'^^xsd:double ;" + "\n"
                         + $"\tbrick:hasUnit  unit:DEG_C ." + "\n"
                         );
                    }

                    //Nominal return watet temperature
                    if (component.Symbol.LookupParameter("NominalMassflow") != null)
                    {
                        string designRetTempID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        string designRetTempValue = component.Symbol.LookupParameter("NominalReturnTemperature").AsString();
                        sb.Append(
                           $"inst:Comp_{componentID} ssn:hasProperty inst:NominalReturnTemperature_{designRetTempID} ." + "\n"
                         + $"inst:NominalReturnTemperature_{designRetTempID} a ssn-ext:NominalReturnTemperature ;" + "\n"
                         + $"\tbrick:value '{designRetTempValue}'^^xsd:double ;" + "\n"
                         + $"\tbrick:hasUnit  unit:DEG_C ." + "\n"
                         );
                    }

                    //Nominal exponential coefficient
                    if (component.Symbol.LookupParameter("ExponentialCoefficient") != null)
                    {
                        string designNID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        string designNValue = component.Symbol.LookupParameter("ExponentialCoefficient").AsString();
                        sb.Append(
                           $"inst:Comp_{componentID} ssn:hasProperty inst:ExponentialCoefficient_{designNID} ." + "\n"
                         + $"inst:ExponentialCoefficient_{designNID} a ssn-ext:ExponentialCoefficient ;" + "\n"
                         + $"\tbrick:value '{designNValue}'^^xsd:double ." + "\n"
                         );
                    }

                    if (component.Space != null)
                    {
                        //string s = component.Space.Name;
                        string relatedRoomID = component.Space.UniqueId.ToString();
                        sb.Append($"inst:Comp_{componentID} fso:transfersHeatTo inst:Space_{relatedRoomID} ." + "\n");
                    }

                    // add sensor data to radiator
                    if (component.LookupParameter("RB_AKS") != null)
                    {
                        if (component.LookupParameter("RB_AKS").AsString() != null)
                        {
                            timeseriesID = component.LookupParameter("RB_AKS").AsString();

                            sb.Append(
                                //make host a feature of interest
                                $"inst:Comp_{componentID} rdf:type sosa:FeatureOfInterest . \n\n"

                            );

                            char[] charVOC = timeseriesID.ToCharArray();
                            charVOC[14] = 'A';
                            charVOC[15] = 'R';

                            char[] charCO2 = timeseriesID.ToCharArray();
                            charCO2[14] = 'O';
                            charCO2[15] = 'T';

                            char[] charABF = timeseriesID.ToCharArray();
                            charABF[14] = 'T';
                            charABF[15] = 'R';


                            string timeseriesID_AR = new string(charVOC);
                            string timeseriesID_OT = new string(charCO2);
                            string timeseriesID_TR = new string(charABF);


                            string measurementID_AR = System.Guid.NewGuid().ToString().Replace(' ', '-');
                            string measurementID_OT = System.Guid.NewGuid().ToString().Replace(' ', '-');
                            string measurementID_TR = System.Guid.NewGuid().ToString().Replace(' ', '-');


                            //connect feature of interest with property instance
                                //property instance: actuator (Stellventil)
                            sb.Append($"inst:Comp_{componentID} ssn:hasProperty inst:AR_{measurementID_AR} . \n\n");
                                //property instance: setpoint (Sollwert Raumtemperatur)
                            sb.Append($"inst:Comp_{componentID} ssn:hasProperty inst:OT_{measurementID_OT} . \n\n");
                                //property instance: sensor (Raumtemperatur)
                            sb.Append($"inst:Comp_{componentID} ssn:hasProperty inst:TR_{measurementID_TR} . \n\n");

                            //create measurement instance
                            sb.Append($"inst:AR_{measurementID_AR} " + "\n" +
                                "\t" + "rdf:type sosa:ActuatableProperty " + " ; \n" +
                                "\t" + $"rdfs:label 'controlled flow from Thermostat' " + ";\n" +
                                ".\n\n"
                                );
                            sb.Append($"inst:OT_{measurementID_OT} " + "\n" +
                                "\t" + "rdf:type sosa:ActuatableProperty " + " ; \n" +
                                //"\t" + "rdf:type brick:Air_Temperature_Setpoint " + " ; \n" +
                                "\t" + $"rdfs:label 'Temperature setpoint for Thermostat' " + ";\n" +
                                ".\n\n"
                                );
                            sb.Append($"inst:TR_{measurementID_TR} " + "\n" +
                                "\t" + "rdf:type sosa:ObservableProperty " + " ; \n" +
                                "\t" + $"rdfs:label 'Data from Thermostat' " + ";\n" +
                                ".\n\n"
                                );

                            //create sensor/actuator/setpoint instance
                            sb.Append($"inst:Actuator_{timeseriesID_AR} " + "  \n" +
                                "\t" + "rdf:type sosa:Actuator " + " ; \n" +
                                "\t" + "rdf:type brick:Water_Flow_Sensor " + " ; \n" +
                                "\t" + $"rdfs:label 'actuator for radiator massflow' " + ";\n" +
                                "\t" + $"ssn:forProperty inst:AR_{measurementID_AR}  " + ";\n" +
                                //    "\t" + $"brick:hasUnit unit:{measurementUnit} " + ";\n" +
                                "\t" + $"ref:hasTimeseriesID '{timeseriesID_AR}' " + ";\n" +
                                "\t" + $"ref:storedAt '{databaseLocation}' " + ";\n" +
                                ".\n\n"
                                );

                            sb.Append($"inst:Setpoint_{timeseriesID_OT} " + "  \n" +
                                "\t" + "rdf:type sosa:Actuator " + " ; \n" +
                                //"\t" + $"rdf:type brick:Temperature_Sensor  " + " ; \n" +
                                "\t" + "rdf:type brick:Air_Temperature_Setpoint " + " ; \n" +
                                "\t" + $"rdfs:label 'temperature setpoint' " + ";\n" +
                                "\t" + $"ssn:forProperty inst:OT_{measurementID_OT}  " + ";\n" +
                                //    "\t" + $"brick:hasUnit unit:{measurementUnit} " + ";\n" +
                                "\t" + $"ref:hasTimeseriesID '{timeseriesID_OT}' " + ";\n" +
                                "\t" + $"ref:storedAt '{databaseLocation}' " + ";\n" +
                                ".\n\n"
                                );

                            sb.Append($"inst:Sensor_{timeseriesID_TR} " + "  \n" +
                                "\t" + "rdf:type sosa:Sensor " + " ; \n" +
                                "\t" + $"rdf:type brick:Temperature_Sensor  " + " ; \n" +
                                "\t" + $"rdfs:label 'virtual Sensor' " + ";\n" +
                                "\t" + $"sosa:observes inst:TR_{measurementID_TR}  " + ";\n" +
                                //    "\t" + $"brick:hasUnit unit:{measurementUnit} " + ";\n" +
                                "\t" + $"ref:hasTimeseriesID '{timeseriesID_TR}' " + ";\n" +
                                "\t" + $"ref:storedAt '{databaseLocation}' " + ";\n" +
                                ".\n\n"
                                );

                            //create timeseries reference instance
                            sb.Append($"inst:TimeRef_{timeseriesRandomID} " + "\n" +
                                "\t" + $"rdf:type ref:TimeseriesReference " + " ; \n" +
                                "\t" + $"ref:hasTimeseriesID '{timeseriesID_AR}' " + " ; \n" +
                                "\t" + $"ref:hasTimeseriesID '{timeseriesID_OT}' " + " ; \n" +
                                "\t" + $"ref:hasTimeseriesID '{timeseriesID_TR}' " + " ; \n" +
                                "\t" + $"ref:storedAt '{databaseLocation}' " + ";\n" +
                                ".\n\n"
                                );

                            string azure_twin_ID_AR;
                            string azure_twin_ID_OT;
                            string azure_twin_ID_TR;

                            azure_twin_ID_AR = timeseriesID_AR.Replace(buildingNumber, string.Empty);
                            azure_twin_ID_OT = timeseriesID_OT.Replace(buildingNumber, string.Empty);
                            azure_twin_ID_TR = timeseriesID_TR.Replace(buildingNumber, string.Empty);

                            sb.Append($"inst:Actuator_{timeseriesID_AR} " + "  \n" +
                                        "\t" + "rdf:type td:Thing " + " ; \n" +
                                        "\t" + $"td:name '{azure_twin_ID_AR}' " + " ; \n" +
                                        "\t" + $"td:hasActionAffordance inst:ActionAffordance_{measurementID_AR}" + ". \n"
                                        );
                            //create td:ActionAffordance  instance
                            sb.Append($"inst:ActionAffordance_{measurementID_AR} a td:ActionAffordance." + "\n\n");

                            sb.Append($"inst:Setpoint_{timeseriesID_OT} " + "  \n" +
                                       "\t" + "rdf:type td:Thing " + " ; \n" +
                                       "\t" + $"td:name '{azure_twin_ID_OT}' " + " ; \n" +
                                       "\t" + $"td:hasActionAffordance inst:ActionAffordance_{measurementID_OT}" + ". \n"
                                      );
                            //create td:Property Affordance  instance
                            sb.Append($"inst:ActionAffordance_{measurementID_OT} a td:ActionAffordance." + "\n\n"
                                );

                            sb.Append($"inst:Sensor_{timeseriesID_TR} " + "  \n" +
                                       "\t" + "rdf:type td:Thing " + " ; \n" +
                                       "\t" + $"td:name '{azure_twin_ID_TR}' " + " ; \n" +
                                       "\t" + $"td:hasPropertyAffordance inst:PropertyAffordance_{measurementID_TR}" + ". \n"
                                      );
                            // create td:Property Affordance  instance
                            sb.Append($"inst:PropertyAffordance_{measurementID_TR} a td:PropertyAffordance." + "\n\n"
                                );


                        }
                    }

                }

                //AirTerminal
                else if (component.Symbol.LookupParameter("IfcExportAs").AsString() == "IfcAirTerminal")
                {
                    //Type
                    sb.Append($"inst:Comp_{componentID} a fso-ext:AirTerminal ;" + "\n"
                        + $"\tprops:hasRevitId '{revitID}'^^xsd:string ." + "\n");

                    if (component.LookupParameter("System Classification").AsString() == "Return Air")
                    {
                        //AirTerminalType
                        string airTerminalTypeID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        sb.Append(
                           $"inst:Comp_{componentID} ssn:hasProperty inst:AirTerminalType_{airTerminalTypeID} ." + "\n"
                         + $"inst:AirTerminalType_{airTerminalTypeID} a ssn-ext:AirTerminalType_Outlet ." + "\n"
                         );

                        if (component.Space != null)
                        {
                            //Relation to room and space
                            string relatedRoomID = component.Space.UniqueId.ToString();
                            //sb.Append($"inst:Space_{relatedRoomID} fso:suppliesFluidTo inst:Comp_{componentID} ." + "\n");
                            sb.Append($"inst:Space_{relatedRoomID} fso:feedsFluidTo inst:Comp_{componentID} ." + "\n");
                        }
                        else if (component.Room != null)
                        {
                            string relatedRoomID = component.Room.UniqueId.ToString();

                            sb.Append($"inst:Space_{relatedRoomID} a bot:Space ." + "\n" +
                                //$"inst:Space_{relatedRoomID} fso:suppliesFluidTo inst:Comp_{componentID} ." + "\n"
                                $"inst:Space_{relatedRoomID} fso:feedsFluidTo inst:Comp_{componentID} ." + "\n"
                                );
                        }
                        //Adding a fictive port the airterminal which is not included in Revit
                        string connectorID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        sb.Append(
                            $"inst:Comp_{componentID} fso:hasPort inst:Port_{connectorID} ." + "\n"
                            + $"inst:Port_{connectorID} a fso:Port ." + "\n"
                            );

                        //Diameter to fictive port 

                        //FlowDirection to fictive port
                        string connectorDirectionID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        //string connectorDirection = "In";

                        sb.Append(
                          $"inst:Comp_{connectorID} ssn:hasProperty inst:FlowDirection_{connectorDirectionID} ." + "\n"
                        + $"inst:FlowDirection_{connectorDirectionID} a ssn-ext:NominalFlowDirection_In ." + "\n"
                        );
                    }


                    if (component.LookupParameter("System Classification").AsString() == "Supply Air")
                    {
                        //AirTerminalType
                        string airTerminalTypeID = System.Guid.NewGuid().ToString().Replace(' ', '-'); ;
                        // string airTerminalTypeValue = "inlet";
                        sb.Append(
                           $"inst:Comp_{componentID} ssn:hasProperty inst:AirTerminalType_{airTerminalTypeID} ." + "\n"
                         + $"inst:AirTerminalType_{airTerminalTypeID} a ssn-ext:AirTerminalType_Inlet ." + "\n"
                         );
                        if (component.Space != null)
                        {
                            //Relation to room and space
                            string relatedRoomID = component.Space.UniqueId.ToString();
                            //sb.Append($"inst:Space_{relatedRoomID} fso:suppliesFluidTo inst:Comp_{componentID} ." + "\n");
                            //sb.Append($"inst:Comp_{componentID} fso:suppliesFluidTo inst:Space_{relatedRoomID} ." + "\n");
                            sb.Append($"inst:Comp_{componentID} fso:feedsFluidTo inst:Space_{relatedRoomID} ." + "\n");
                        }

                        else if (component.Room != null)
                        {
                            //Relation to room and space
                            string relatedRoomID = component.Room.UniqueId.ToString();
                            //sb.Append($"inst:Comp_{componentID} fso:suppliesFluidTo inst:Space_{relatedRoomID} ." + "\n");
                            sb.Append($"inst:Comp_{componentID} fso:feedsFluidTo inst:Space_{relatedRoomID} ." + "\n");
                        }

                        //Adding a fictive port the airterminal which is not included in Revit
                        string connectorID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        sb.Append(
                              $"inst:Comp_{componentID} fso:hasPort inst:Port_{connectorID} ." + "\n"
                            + $"inst:Port_{connectorID} a fso:Port ." + "\n"
                        );

                        //FlowDirection
                        string connectorDirectionID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        // string connectorDirection = "Out";

                        sb.Append(
                          $"inst:Comp_{connectorID} ssn:hasProperty inst:FlowDirection_{connectorDirectionID} ." + "\n"
                        + $"inst:FlowDirection_{connectorDirectionID} a ssn-ext:NominalFlowDirection_Out ." + "\n"
                        );


                        //Fictive pressureDrop
                        string pressureDropID = System.Guid.NewGuid().ToString().Replace(' ', '-'); ;
                        double pressureDropValue = 5;
                        sb.Append($"inst:{connectorID} ssn:hasProperty inst:PressureDrop_{pressureDropID} ." + "\n"
                       + $"inst:PressureDrop_{pressureDropID} a ssn-ext:NominalPressureDrop ;" + "\n"
                       + $"\tbrick:value '{pressureDropValue}'^^xsd:double ;" + "\n"
                       + $"\tbrick:hasUnit unit:PA ." + "\n");

                    }
                }

                //Heat exchanger
                else if (component.Symbol.LookupParameter("IfcExportAs").AsString() == "IfcHeatExchanger")
                {
                    string hostname = null;
                    string hostGuid = null;
                    string hostGraphID = null;
                    string hostRevitID = null;

                    string measurementID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                    string measurementType = "Flow";

                    string sensorID = System.Guid.NewGuid().ToString().Replace(' ', '-');

                    string measurementUnit = "0";
                    string timeseriesID = "0";
                    string timeseriesRandomID = System.Guid.NewGuid().ToString().Replace(' ', '-');


                    sb.Append($"inst:Comp_{componentID} a fso-ext:HeatExchanger ;" + "\n"
                        + $"\tprops:hasRevitId '{revitID}'^^xsd:string ." + "\n");

                    if (component.LookupParameter("NominalPower") != null)
                    {
                        //DesignHeatPower
                        string designHeatPowerID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        double designHeatPowerValue = Math.Round(UnitUtils.ConvertFromInternalUnits(component.LookupParameter("NominalPower").AsDouble(), UnitTypeId.Watts), 2, MidpointRounding.ToEven);
                        sb.Append($"inst:Comp_{componentID} ssn:hasProperty inst:NominalHeatingPower_{designHeatPowerID} ." + "\n"
                         + $"inst:NominalHeatingPower_{designHeatPowerID} a ssn-ext:NominalPower ;" + "\n"
                         + $"\tbrick:value  '{designHeatPowerValue}'^^xsd:double ;" + "\n"
                         + $"\tbrick:hasUnit  unit:W ." + "\n");
                    }

                    if (component.LookupParameter("RB_AKS") != null)
                    {
                        if (component.LookupParameter("RB_AKS").AsString() != null)
                        {
                            timeseriesID = component.LookupParameter("RB_AKS").AsString();

                            sb.Append(
                                //make host a feature of interest
                                $"inst:Comp_{componentID} rdf:type sosa:FeatureOfInterest . \n" +
                                //connect feature of interest with measurement instance,
                                $"inst:Comp_{componentID} ssn:forProperty inst:{measurementType}_{measurementID} . \n\n"
                            );

                            //create measurement instance
                            sb.Append($"inst:{measurementType}_{measurementID} " + "\n" +
                                "\t" + "rdf:type sosa:ActuatableProperty " + " ; \n" +
                                "\t" + $"rdfs:label '{measurementType} {measurementID}' " + ";\n" +
                                ".\n\n"
                                );


                            //create sensor instance
                            //sb.Append($"inst:Actuator_{sensorID} " + "  \n" +
                            sb.Append($"inst:Actuator_{timeseriesID} " + "  \n" +
                                "\t" + "rdf:type sosa:Actuator " + " ; \n" +
                                "\t" + "rdf:type brick:Water_Flow_Sensor " + " ; \n" +
                                "\t" + $"rdfs:label 'virtual actuator' " + ";\n" +
                                "\t" + $"sosa:observes inst:{measurementType}_{measurementID} " + ";\n" +
                                //    "\t" + $"brick:hasUnit unit:{measurementUnit} " + ";\n" +
                                "\t" + $"ref:hasTimeseriesID '{timeseriesID}' " + ";\n" +
                                "\t" + $"ref:storedAt '{databaseLocation}' " + ";\n" +
                                ".\n\n"
                                );

                            //create timeseries reference instance
                            sb.Append($"inst:TimeRef_{timeseriesRandomID} " + "\n" +
                                "\t" + $"rdf:type ref:TimeseriesReference " + " ; \n" +
                                "\t" + $"ref:hasTimeseriesID '{timeseriesID}' " + " ; \n" +
                                "\t" + $"ref:storedAt '{databaseLocation}' " + ";\n" +
                                ".\n\n"
                                );

                            if (component.LookupParameter("Azure_Twin_ID") != null)
                            {

                                azure_twin_ID = component.LookupParameter("Azure_Twin_ID").AsString();

                                sb.Append($"inst:Actuator_{timeseriesID} " + "  \n" +
                                    "\t" + "rdf:type td:Thing " + " ; \n" +
                                    "\t" + $"td:name '{azure_twin_ID}' " + " ; \n" +
                                    "\t" + $"td:hasActionAffordance inst:ActionAffordance_{timeseriesRandomID}" + ". \n"
                                    );
                                //create td:ActionAffordance  instance
                                sb.Append($"inst:ActionAffordance_{timeseriesRandomID} a td:ActionAffordance." + "\n\n"
                                    );

                            }
                            else
                            {
                                azure_twin_ID = timeseriesID.Replace(buildingNumber, string.Empty);
                                sb.Append($"inst:Actuator_{timeseriesID} " + "  \n" +
                                           "\t" + "rdf:type td:Thing " + " ; \n" +
                                           "\t" + $"td:name '{azure_twin_ID}' " + " ; \n" +
                                           "\t" + $"td:hasActionAffordance inst:ActionAffordance_{timeseriesRandomID}" + ". \n"
                                          );
                                //create td:ActionAffordance  instance
                                sb.Append($"inst:ActionAffordance_{timeseriesRandomID} a td:ActionAffordance." + "\n\n"
                                    );
                            }

                        }
                    }
                    //RelatedPorts.HeatExchangerConnectors(component, componentID, sb);
                }

                // heating distributor
                else if (component.Symbol.LookupParameter("IfcExportAs").AsString() == "IfcDistributionElement")
                {

                    //Type
                    sb.Append($"inst:Comp_{componentID} a fso-ext:DistributionElement ;" + "\n"
                        + $"\tprops:hasRevitId '{revitID}'^^xsd:string ." + "\n");

                    //DesignHeatPower
                    if (component.LookupParameter("NominalHeatingPower") != null)
                    {
                        string designHeatPowerID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        double designHeatPowerValue = Math.Round(UnitUtils.ConvertFromInternalUnits(component.LookupParameter("NominalHeatingPower").AsDouble(), UnitTypeId.Watts), 2, MidpointRounding.ToEven);
                        sb.Append(
                           $"inst:Comp_{componentID} ssn:hasProperty inst:NominalHeatingPower_{designHeatPowerID} ." + "\n"
                         + $"inst:NominalHeatingPower_{designHeatPowerID} a ssn-ext:NominalPower ;" + "\n"
                         + $"\tbrick:value '{designHeatPowerValue}'^^xsd:double ;" + "\n"
                         + $"\tbrick:hasUnit  unit:W ." + "\n"
                         );
                    }


                }

                // boiler
                else if (component.Symbol.LookupParameter("IfcExportAs").AsString() == "IfcBoiler")
                {

                    //Type
                    sb.Append($"inst:Comp_{componentID} a fso-ext:Boiler ;" + "\n"
                        + $"\tprops:hasRevitId '{revitID}'^^xsd:string ." + "\n");

                    //DesignHeatPower
                    if (component.LookupParameter("NominalPower") != null)
                    {
                        string designHeatPowerID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        double designHeatPowerValue = Math.Round(UnitUtils.ConvertFromInternalUnits(component.LookupParameter("NominalPower").AsDouble(), UnitTypeId.Watts));
                        sb.Append(
                           $"inst:Comp_{componentID} ssn:hasProperty inst:NominalHeatingPower_{designHeatPowerID} ." + "\n"
                         + $"inst:NominalHeatingPower_{designHeatPowerID} a ssn-ext:NominalPower ;" + "\n"
                         + $"\tbrick:value '{designHeatPowerValue}'^^xsd:double ;" + "\n"
                         + $"\tbrick:hasUnit  unit:W ." + "\n"
                         );
                    }


                    //design massflow
                    if (component.LookupParameter("NominalMassflow") != null)
                    {
                        string designMassflowID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        double designMassflowValue = Math.Round(UnitUtils.ConvertFromInternalUnits(component.LookupParameter("NominalMassflow").AsDouble(), UnitTypeId.LitersPerSecond), 2, MidpointRounding.ToEven);
                        sb.Append(
                           $"inst:Comp_{componentID} ssn:hasProperty inst:NominalMassflow_{designMassflowID} ." + "\n"
                         + $"inst:NominalMassflow_{designMassflowID} a ssn-ext:NominalMassflow ;" + "\n"
                         + $"\tbrick:value '{designMassflowValue}'^^xsd:double ;" + "\n"
                         + $"\tbrick:hasUnit  unit:Lps ." + "\n"
                         );
                    }

                    //design massflow
                    if (component.LookupParameter("Efficiency") != null)
                    {
                        string designEfficiencyID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                        double designEfficiencyValue = Math.Round(component.LookupParameter("Efficiency").AsDouble(), 2, MidpointRounding.ToEven);
                        sb.Append(
                           $"inst:Comp_{componentID} ssn:hasProperty inst:Efficiency_{designEfficiencyID} ." + "\n"
                         + $"inst:Efficiency_{designEfficiencyID} a ssn-ext:Efficiency ;" + "\n"
                         + $"\tbrick:value '{designEfficiencyValue}'^^xsd:double ;" + "\n"
                         + $"\tbrick:hasUnit  unit:Lps ." + "\n"
                         );
                    }


                }

                //DuctSilencer
                else if (component.Symbol.LookupParameter("IfcExportAs").AsString() == "IfcDuctSilencer")
                {
                    //Type 
                    sb.Append(
                      $"inst:Comp_{componentID} a fso-ext:DuctSilencer ;" + "\n"
                      + $"\tprops:hasRevitId '{revitID}'^^xsd:string ." + "\n"
                    );
                }


                //SensorFitting
                else if (component.Symbol.LookupParameter("IfcExportAs").AsString() == "IfcSensorFitting")
                {

                    //Type
                    sb.Append($"inst:Comp_{componentID} a fso-ext:SensorFitting ;" + "\n"
                        + $"\tprops:hasRevitId '{revitID}'^^xsd:string ." + "\n");

                }

                //Flowmeter
                else if (component.Symbol.LookupParameter("IfcExportAs").AsString() == "IfcFlowMeter")
                {
                    string hostname = null;
                    string hostGuid = null;
                    string hostGraphID = null;
                    string hostRevitID = null;

                    string measurementID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                    string measurementType = "Flow";

                    string sensorID = System.Guid.NewGuid().ToString().Replace(' ', '-');

                    string brickSensor = "Water_Flow_Sensor";
                    string measurementUnit = "0";
                    string timeseriesID = "0";
                    string timeseriesRandomID = System.Guid.NewGuid().ToString().Replace(' ', '-');

                    string componentTypeEnum = component.Symbol.LookupParameter("IfcExportType").AsString();
                    if (componentTypeEnum == "WATERMETER")
                    {
                        //Type
                        sb.Append($"inst:Comp_{componentID} a fso-ext:Flowmeter ;" + "\n"
                        + $"\tprops:hasRevitId '{revitID}'^^xsd:string ." + "\n");
                    }

                    if (component.LookupParameter("RB_AKS") != null)
                    {
                        if (component.LookupParameter("RB_AKS").AsString() != null)
                        {
                            timeseriesID = component.LookupParameter("RB_AKS").AsString();

                            sb.Append(
                                //make host a feature of interest
                                $"inst:Comp_{componentID} rdf:type sosa:FeatureOfInterest . \n" +
                                //connect feature of interest with measurement instance,
                                $"inst:Comp_{componentID} ssn:hasProperty inst:{measurementType}_{measurementID} . \n\n"
                            );

                            //create measurement instance
                            sb.Append($"inst:{measurementType}_{measurementID} " + "\n" +
                                "\t" + "rdf:type sosa:ObservableProperty " + " ; \n" +
                                "\t" + $"rdfs:label '{measurementType} {measurementID}' " + ";\n" +
                                ".\n\n"
                                );


                            //create sensor instance
                            //sb.Append($"inst:Sensor_{sensorID} " + "  \n" +
                            sb.Append($"inst:Sensor_{timeseriesID} " + "  \n" +
                                "\t" + "rdf:type sosa:Sensor " + " ; \n" +
                                "\t" + $"rdf:type brick:{brickSensor} " + " ; \n" +
                                "\t" + $"rdfs:label 'virtual sensor' " + ";\n" +
                                "\t" + $"sosa:observes inst:{measurementType}_{measurementID} " + ";\n" +
                                //    "\t" + $"brick:hasUnit unit:{measurementUnit} " + ";\n" +
                                "\t" + $"ref:hasTimeseriesID '{timeseriesID}' " + ";\n" +
                                "\t" + $"ref:storedAt '{databaseLocation}' " + ";\n" +
                                ".\n\n"
                                );

                            //create timeseries reference instance
                            sb.Append($"inst:TimeRef_{timeseriesRandomID} " + "\n" +
                                "\t" + $"rdf:type ref:TimeseriesReference " + " ; \n" +
                                "\t" + $"ref:hasTimeseriesID '{timeseriesID}' " + " ; \n" +
                                "\t" + $"ref:storedAt '{databaseLocation}' " + ";\n" +
                                ".\n\n"
                                );

                            if (component.LookupParameter("Azure_Twin_ID") != null)
                            {

                                azure_twin_ID = component.LookupParameter("Azure_Twin_ID").AsString();

                                sb.Append($"inst:Sensor_{timeseriesID} " + "  \n" +
                                    "\t" + "rdf:type td:Thing" + " ; \n" +
                                    "\t" + $"td:name '{azure_twin_ID}' " + " ; \n" +
                                    "\t" + $"td:hasPropertyAffordance inst:PropertyAffordance_{timeseriesRandomID}" + ". \n"
                                    );
                                //create td:PropertyAffordance  instance
                                sb.Append($"inst:PropertyAffordance_{timeseriesRandomID} a td:PropertyAffordance." + "\n\n"
                                    );

                            }
                            else
                            {
                                azure_twin_ID = timeseriesID.Replace(buildingNumber, string.Empty);
                                sb.Append($"inst:Sensor_{timeseriesID} " + "  \n" +
                                           "\t" + "rdf:type td:Thing " + " ; \n" +
                                           "\t" + $"td:name '{azure_twin_ID}' " + " ; \n" +
                                           "\t" + $"td:hasPropertyAffordance inst:PropertyAffordance_{timeseriesRandomID}" + ". \n"
                                          );
                                // create td:PropertyAffordance  instance
                                sb.Append($"inst:PropertyAffordance_{timeseriesRandomID} a td:PropertyAffordance." + "\n\n"
                                    );
                            }
                        }
                    }

                }

                else if (component.Symbol.LookupParameter("IfcExportAs").AsString() == "IfcAHUFan")
                {
                    // do noting
                }

                //Chiller
                else if (component.Symbol.LookupParameter("IfcExportAs").AsString() == "IfcChiller")
                {

                    //Type
                    sb.Append($"inst:Comp_{componentID} a fso-ext:Chiller ;" + "\n"
                        + $"\tprops:hasRevitId '{revitID}'^^xsd:string ." + "\n");

                }

                //CoolingCoil
                else if (component.Symbol.LookupParameter("IfcExportAs").AsString() == "IfcCoil")
                {

                    //Type
                    sb.Append($"inst:Comp_{componentID} a fso-ext:CoolingCoil ;" + "\n"
                        + $"\tprops:hasRevitId '{revitID}'^^xsd:string ." + "\n");

                }

                else
                {
                    sb.Append($"inst:Comp_{componentID} a fso:Component ;" + "\n"
                         + $"\tprops:hasRevitId '{revitID}'^^xsd:string ." + "\n");
                }

                //if (component.Symbol.LookupParameter("IfcExportAs").AsString() == "IfcAHU")
                //{
                    RelatedPorts.GenericConnectors(component, revitID, componentID, sb);
                //}
            }


            return Result.Succeeded;
        }

        public Result ParseSensor(FamilyInstance component, StringBuilder sb, Autodesk.Revit.DB.Document doc)
        {
            string componentID = component.UniqueId;
            string revitID = component.Id.ToString();

            //change: if IfcExportAs = IfcSensor
            if(component.Symbol.LookupParameter("IfcExportAs") != null)
            {
                //Type
                string componentType = component.Symbol.LookupParameter("IfcExportAs").AsString();
                if (componentType == "IfcSensor")
                //if (component.Category.Name == "Communication Devices")
                {
                
                    string hostname = null;
                    string hostGuid = null;
                    string hostGuid_MEP = null;
                    string hostGraphID = null;
                    string hostRevitID = null;

                    string measurementID = System.Guid.NewGuid().ToString().Replace(' ', '-');
                    string measurementType = null;

                    string sensorID = componentID;
                    string sensorRevitID =component.Id.ToString();

                    string brickSensor = "0";
                    string measurementUnit = "0";
                    string timeseriesID = "0";
                    string timeseriesRandomID = System.Guid.NewGuid().ToString().Replace(' ', '-');

                    int numberOfSensors = 0;

                    // find host of sensor
                    if (component.Host != null)
                    {
                        // Sensor for rooms
                        if (component.Host.Category.Name.ToString() == "Levels")
                        {
                            if (component.Room != null)
                            {
                                //host
                                hostname = component.Room.ToString();
                                hostname = component.Space.ToString();

                                //host guid
                                hostGuid = component.Room.UniqueId.ToString();
                                hostGuid = component.Space.UniqueId.ToString();

                                //host graph id
                                // ********** change to MEP space **********
                                hostGraphID = $"Space_{hostGuid}";

                                // host revit id
                                hostRevitID = component.Room.Id.ToString();
                                hostRevitID = component.Space.Id.ToString();
                            }
                        }


                        // sensor for components
                        else
                        {
                            //host
                            hostname = component.Host.ToString();

                            //host guid
                            hostGuid = component.Host.UniqueId.ToString();

                            //host graph id
                            hostGraphID = $"Comp_{hostGuid}";

                            // host revit id
                            hostRevitID = component.Host.Id.ToString();
                        }
                    }

                    // sensor for rooms if no host available
                    else if (component.Room != null)
                    {
                        //host
                        hostname = component.Room.ToString();
                        hostname = component.Space.ToString();

                        //host guid
                        hostGuid = component.Room.UniqueId.ToString();
                        hostGuid = component.Space.UniqueId.ToString();

                        //host graph id
                        hostGraphID = $"Space_{hostGuid}";

                        // host revit id
                        hostRevitID = component.Room.Id.ToString();
                        hostRevitID = component.Space.Id.ToString();
                    }

                    if (hostname != null)
                    {
                        // ********** change to IFC export parameter **********
                        //get measurement type
                        // change: if IfcExportType = TEMPERATURESENSOR
                        if (component.Symbol.LookupParameter("IfcExportAs") != null)
                        {
                            string componentTypeEnum = component.Symbol.LookupParameter("IfcExportType").AsString();
                            if (componentTypeEnum == "TEMPERATURESENSOR")
                            //if (component.Symbol.FamilyName == "Temperatursensor")
                            {
                                measurementType = "Temp";
                                brickSensor = "Temperature_Sensor";
                                numberOfSensors = 1;
                            }

                            // change: if IfcExportType = KOMBISENSOR
                            else if (componentTypeEnum == "KOMBISENSOR")
                            //else if (component.Symbol.FamilyName == "Kombisensor")
                            {
                                measurementType = "Kombi";
                                brickSensor = "Air_Quality_Sensor";
                                numberOfSensors = 5;
                            }

                            // change: if IfcExportType = PRESSURESENSOR
                            else if (componentTypeEnum == "PRESSURESENSOR")
                            //else if (component.Symbol.FamilyName == "Drucksensor")
                            {
                                measurementType = "P";
                                brickSensor = "Static_Pressure_Sensor";
                                numberOfSensors = 1;
                            }

                            // change: if IfcExportType = PRESSURESENSOR
                            else if (componentTypeEnum == "PRESSURESENSOR")
                            //else if (component.Symbol.FamilyName == "Differenzdrucksensor")
                            {
                                measurementType = "DP";
                                brickSensor = "Differential_Pressure_Sensor";
                                numberOfSensors = 1;
                            }

                            else
                            {

                            }

                            //get timeseries ID
                            if (component.LookupParameter("RB_AKS") != null)
                            {
                                //change: remove duplicated LookupParameter
                                if (component.LookupParameter("RB_AKS").AsString() != null)
                                {



                                    //make host a feature of interest 
                                    sb.Append($"inst:{hostGraphID} rdf:type sosa:FeatureOfInterest . \n");

                                    //Kombi Sensors
                                    if (measurementType == "Kombi")
                                    {
                                        timeseriesID = component.LookupParameter("RB_AKS").AsString();
                                        string timeseriesID_template = timeseriesID.Substring(0, timeseriesID.LastIndexOf('.'));
                                        string timeseriesID_VOC = timeseriesID_template + ".QL01";
                                        string timeseriesID_CO2 = timeseriesID_template + ".QR01";
                                        string timeseriesID_ABF = timeseriesID_template + ".FR01";
                                        string timeseriesID_ABT = timeseriesID_template + ".TR01";

                                        string measurementID_VOC = System.Guid.NewGuid().ToString().Replace(' ', '-');
                                        string measurementID_CO2 = System.Guid.NewGuid().ToString().Replace(' ', '-');
                                        string measurementID_ABF = System.Guid.NewGuid().ToString().Replace(' ', '-');
                                        string measurementID_ABT = System.Guid.NewGuid().ToString().Replace(' ', '-');

                                        //connect feature of interest with measurement instance
                                        sb.Append($"inst:{hostGraphID} ssn:hasProperty inst:VOC_{measurementID_VOC} . \n\n");
                                        sb.Append($"inst:{hostGraphID} ssn:hasProperty inst:CO2_{measurementID_CO2} . \n\n");
                                        sb.Append($"inst:{hostGraphID} ssn:hasProperty inst:ABF_{measurementID_ABF} . \n\n");
                                        sb.Append($"inst:{hostGraphID} ssn:hasProperty inst:ABT_{measurementID_ABT} . \n\n");

                                        //create measurement instance
                                        sb.Append($"inst:VOC_{measurementID_VOC} " + "\n" +
                                            "\t" + "rdf:type sosa:ObservableProperty " + " ; \n" +
                                            "\t" + $"rdfs:label 'Data from Kombisensor' " + ";\n" +
                                            ".\n\n"
                                            );
                                        sb.Append($"inst:CO2_{measurementID_CO2} " + "\n" +
                                            "\t" + "rdf:type sosa:ObservableProperty " + " ; \n" +
                                            "\t" + $"rdfs:label 'Data from Kombisensor' " + ";\n" +
                                            ".\n\n"
                                            );
                                        sb.Append($"inst:ABF_{measurementID_ABF} " + "\n" +
                                            "\t" + "rdf:type sosa:ObservableProperty " + " ; \n" +
                                            "\t" + $"rdfs:label 'Data from Kombisensor' " + ";\n" +
                                            ".\n\n"
                                            );
                                        sb.Append($"inst:ABT_{measurementID_ABT} " + "\n" +
                                            "\t" + "rdf:type sosa:ObservableProperty " + " ; \n" +
                                            "\t" + $"rdfs:label 'Data from Kombisensor' " + ";\n" +
                                            ".\n\n"
                                            );



                                        //create sensor instance
                                        sb.Append($"inst:Sensor_{timeseriesID_VOC} " + "  \n" +
                                            "\t" + "rdf:type sosa:Sensor " + " ; \n" +
                                            //"\t" + $"rdf:type brick:{brickSensor} " + " ; \n" +
                                            "\t" + $"rdf:type brick:CO_Sensor " + " ; \n" +
                                            "\t" + $"rdfs:label '{sensorRevitID}' " + ";\n" +
                                            "\t" + $"sosa:observes inst:VOC_{measurementID_VOC} " + ";\n" +
                                            //    "\t" + $"brick:hasUnit unit:{measurementUnit} " + ";\n" +
                                            "\t" + $"ref:hasTimeseriesID '{timeseriesID_VOC}' " + ";\n" +
                                            "\t" + $"ref:storedAt '{databaseLocation}' " + ";\n" +
                                            ".\n\n"
                                            );

                                        sb.Append($"inst:Sensor_{timeseriesID_CO2} " + "  \n" +
                                            "\t" + "rdf:type sosa:Sensor " + " ; \n" +
                                            //"\t" + $"rdf:type brick:{brickSensor} " + " ; \n" +
                                            "\t" + $"rdf:type brick:CO2_Sensor " + " ; \n" +
                                            "\t" + $"rdfs:label '{sensorRevitID}' " + ";\n" +
                                            "\t" + $"sosa:observes inst:CO2_{measurementID_CO2} " + ";\n" +
                                            //    "\t" + $"brick:hasUnit unit:{measurementUnit} " + ";\n" +
                                            "\t" + $"ref:hasTimeseriesID '{timeseriesID_CO2}' " + ";\n" +
                                            "\t" + $"ref:storedAt '{databaseLocation}' " + ";\n" +
                                            ".\n\n"
                                            );

                                        sb.Append($"inst:Sensor_{timeseriesID_ABF} " + "  \n" +
                                            "\t" + "rdf:type sosa:Sensor " + " ; \n" +
                                            //"\t" + $"rdf:type brick:{brickSensor} " + " ; \n" +
                                            "\t" + $"rdf:type brick:Humidity_Sensor " + " ; \n" +
                                            "\t" + $"rdfs:label '{sensorRevitID}' " + ";\n" +
                                            "\t" + $"sosa:observes inst:ABF_{measurementID_ABF} " + ";\n" +
                                            //    "\t" + $"brick:hasUnit unit:{measurementUnit} " + ";\n" +
                                            "\t" + $"ref:hasTimeseriesID '{timeseriesID_ABF}' " + ";\n" +
                                            "\t" + $"ref:storedAt '{databaseLocation}' " + ";\n" +
                                            ".\n\n"
                                            );

                                        sb.Append($"inst:Sensor_{timeseriesID_ABT} " + "  \n" +
                                            "\t" + "rdf:type sosa:Sensor " + " ; \n" +
                                            //"\t" + $"rdf:type brick:{brickSensor} " + " ; \n" +
                                            "\t" + $"rdf:type brick:Temperature_Sensor " + " ; \n" +
                                            "\t" + $"rdfs:label '{sensorRevitID}' " + ";\n" +
                                            "\t" + $"sosa:observes inst:ABT_{measurementID_ABT} " + ";\n" +
                                            //    "\t" + $"brick:hasUnit unit:{measurementUnit} " + ";\n" +
                                            "\t" + $"ref:hasTimeseriesID '{timeseriesID_ABT}' " + ";\n" +
                                            "\t" + $"ref:storedAt '{databaseLocation}' " + ";\n" +
                                            ".\n\n"
                                            );


                                        //create timeseries reference instance
                                        sb.Append($"inst:TimeRef_{timeseriesRandomID} " + "\n" +
                                            "\t" + $"rdf:type ref:TimeseriesReference " + " ; \n" +
                                            "\t" + $"ref:hasTimeseriesID '{timeseriesID}' " + " ; \n" +
                                            "\t" + $"ref:storedAt '{databaseLocation}' " + ";\n" +
                                            ".\n\n"
                                            );

                                        string azure_twin_ID_VOC;
                                        string azure_twin_ID_CO2;
                                        string azure_twin_ID_ABF;
                                        string azure_twin_ID_ABT;
                                        azure_twin_ID_VOC = timeseriesID_VOC.Replace(buildingNumber, string.Empty);
                                        azure_twin_ID_CO2 = timeseriesID_CO2.Replace(buildingNumber, string.Empty);
                                        azure_twin_ID_ABF = timeseriesID_ABF.Replace(buildingNumber, string.Empty);
                                        azure_twin_ID_ABT = timeseriesID_ABT.Replace(buildingNumber, string.Empty);

                                        sb.Append($"inst:Sensor_{timeseriesID_VOC} " + "  \n" +
                                                   "\t" + "rdf:type td:Thing " + " ; \n" +
                                                   "\t" + $"td:name '{azure_twin_ID_VOC}' " + " ; \n" +
                                                   "\t" + $"td:hasPropertyAffordance inst:PropertyAffordance_{measurementID_VOC}" + ". \n"
                                                  );
                                        // create td:PropertyAffordance  instance
                                        sb.Append($"inst:PropertyAffordance_{measurementID_VOC} a td:PropertyAffordance." + "\n\n"
                                            );


                                        sb.Append($"inst:Sensor_{timeseriesID_CO2} " + "  \n" +
                                                   "\t" + "rdf:type td:Thing " + " ; \n" +
                                                   "\t" + $"td:name '{azure_twin_ID_CO2}' " + " ; \n" +
                                                   "\t" + $"td:hasPropertyAffordance inst:PropertyAffordance_{measurementID_CO2}" + ". \n"
                                                  );
                                        // create td:PropertyAffordance  instance
                                        sb.Append($"inst:PropertyAffordance_{measurementID_ABF} a td:PropertyAffordance." + "\n\n"
                                            );

                                        sb.Append($"inst:Sensor_{timeseriesID_ABF} " + "  \n" +
                                                   "\t" + "rdf:type td:Thing " + " ; \n" +
                                                   "\t" + $"td:name '{azure_twin_ID_ABF}' " + " ; \n" +
                                                   "\t" + $"td:hasPropertyAffordance inst:PropertyAffordance_{measurementID_ABF}" + ". \n"
                                                  );
                                        // create td:PropertyAffordance  instance
                                        sb.Append($"inst:PropertyAffordance_{measurementID_ABF} a td:PropertyAffordance." + "\n\n"
                                            );

                                        sb.Append($"inst:Sensor_{timeseriesID_ABT} " + "  \n" +
                                                   "\t" + "rdf:type td:Thing " + " ; \n" +
                                                   "\t" + $"td:name '{azure_twin_ID_ABT}' " + " ; \n" +
                                                   "\t" + $"td:hasPropertyAffordance inst:PropertyAffordance_{measurementID_ABT}" + ". \n"
                                                  );
                                        // create td:PropertyAffordance  instance
                                        sb.Append($"inst:PropertyAffordance_{measurementID_ABT} a td:PropertyAffordance." + "\n\n"
                                            );

                                    }
                                    //all other sensors
                                    else
                                    {
                                        timeseriesID = component.LookupParameter("RB_AKS").AsString();

                                        //connect feature of interest with measurement instance,
                                        sb.Append($"inst:{hostGraphID} ssn:hasProperty inst:{measurementType}_{measurementID} . \n\n");

                                        //create measurement instance
                                        sb.Append($"inst:{measurementType}_{measurementID} " + "\n" +
                                            "\t" + "rdf:type sosa:ObservableProperty " + " ; \n" +
                                            "\t" + $"rdfs:label '{measurementType} {measurementID}' " + ";\n" +
                                            ".\n\n"
                                            );


                                        //create sensor instance
                                        //sb.Append($"inst:Sensor_{sensorID} " + "  \n" +
                                        sb.Append($"inst:Sensor_{timeseriesID} " + "  \n" +
                                            "\t" + "rdf:type sosa:Sensor " + " ; \n" +
                                            "\t" + $"rdf:type brick:{brickSensor} " + " ; \n" +
                                            "\t" + $"rdfs:label '{sensorRevitID}' " + ";\n" +
                                            "\t" + $"sosa:observes inst:{measurementType}_{measurementID} " + ";\n" +
                                            //    "\t" + $"brick:hasUnit unit:{measurementUnit} " + ";\n" +
                                            "\t" + $"ref:hasTimeseriesID '{timeseriesID}' " + ";\n" +
                                            "\t" + $"ref:storedAt '{databaseLocation}' " + ";\n" +
                                            ".\n\n"
                                            );

                                        //create timeseries reference instance
                                        sb.Append($"inst:TimeRef_{timeseriesRandomID} " + "\n" +
                                            "\t" + $"rdf:type ref:TimeseriesReference " + " ; \n" +
                                            "\t" + $"ref:hasTimeseriesID '{timeseriesID}' " + " ; \n" +
                                            "\t" + $"ref:storedAt '{databaseLocation}' " + ";\n" +
                                            ".\n\n"
                                            );

                                        if (component.LookupParameter("Azure_Twin_ID") != null)
                                        {

                                            azure_twin_ID = component.LookupParameter("Azure_Twin_ID").AsString();

                                            sb.Append($"inst:Sensor_{timeseriesID} " + "  \n" +
                                                "\t" + "rdf:type td:Thing" + " ; \n" +
                                                "\t" + $"td:name '{azure_twin_ID}' " + " ; \n" +
                                                "\t" + $"td:hasPropertyAffordance inst:PropertyAffordance_{timeseriesRandomID}" + ". \n"
                                                );
                                            //create td:PropertyAffordance  instance
                                            sb.Append($"inst:PropertyAffordance_{timeseriesRandomID} a td:PropertyAffordance." + "\n\n"
                                                );

                                        }
                                        else
                                        {
                                            azure_twin_ID = timeseriesID.Replace(buildingNumber, string.Empty);
                                            sb.Append($"inst:Sensor_{timeseriesID} " + "  \n" +
                                                       "\t" + "rdf:type td:Thing " + " ; \n" +
                                                       "\t" + $"td:name '{azure_twin_ID}' " + " ; \n" +
                                                       "\t" + $"td:hasPropertyAffordance inst:PropertyAffordance_{timeseriesRandomID}" + ". \n"
                                                      );
                                            // create td:PropertyAffordance  instance
                                            sb.Append($"inst:PropertyAffordance_{timeseriesRandomID} a td:PropertyAffordance." + "\n\n"
                                                );
                                        }
                                        }
                                    }
                            }
                        }
                    }


                }
            }



            return Result.Succeeded;
        }
    }
}