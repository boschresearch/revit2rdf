# RevitToRDF

This is the companion code for the study reported in the paper entitled with **"Enabling scalable MPC design for building HVAC systems using semantic data modelling"**, under submission to the journal of [Automation in Construction](https://www.sciencedirect.com/journal/automation-in-construction). Preprinit is available [here](https://papers.ssrn.com/sol3/papers.cfm?abstract_id=4875326).
To populate the proposed information model with real building data, a software tool is introduced to translate the 3D Building Information Model (BIM) in format of Revit into an RDF graph. The tool was developed based on the previous open-source project [RevitToRDFConverter](https://github.com/Semantic-HVAC-Tool/Parser), under the MIT license. 

## Purpose of the project

This software is a research prototype, solely developed for and published as part of the publication. It will neither be maintained nor monitored in any way. The goal of this software tool is to extracts the required metadata from the BIM, and
represents the data using the proposed data schema ([semantics4MPC](https://github.com/boschresearch/semantics4mpc)), so that it could enable more scalable MPC algorithm design and deployment in the building industry. Compared with the original parser, the main contribution of the software include the following features:

1. extract detailed geometry of thermal zones from buildings,
1. extract actuator and sensor metadata,
1. extract type, design properties as well as the dynamic properties of the Heating Ventilation and Air-Conditioning components,
1. retrieve functional information of the HVAC systems, and
1. represent the required information using multiple open source ontologies.

## Requirements


## License

Benchmarks is open-sourced under the MIT license. See the
[LICENSE](LICENSE) file for details.


## Acknowledgement
Sincere and special thanks to  [Ali Kücükavci](https://orcid.org/0000-0001-9883-4633) who open-sourced the initial development code for the great help in the further development of this RevitToRDF convertor. 
