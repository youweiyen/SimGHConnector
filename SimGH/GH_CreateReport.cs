using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;
using SimScale.Sdk.Api;
using SimScale.Sdk.Model;

namespace SimGH
{
    public class GH_CreateReport : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the GH_CreateReport class.
        /// </summary>
        public GH_CreateReport()
          : base("CreateReport", "R",
              "Description",
              "SimGH", "1_SimScale")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
        //    // Create report
        //    var scalarField = new ScalarField(
        //        fieldName: "Temperature",
        //        dataType: DataType.CELL
        //    );
        //    var reportRequest = new ReportRequest(
        //        name: "Report 1",
        //        description: "Simulation report",
        //        resultIds: new List<Guid?> {
        //        solutionInfo.ResultId
        //        },
        //        reportProperties: new ScreenshotReportProperties(
        //            modelSettings: new ModelSettings(
        //                scalarField: scalarField
        //            ),
        //            filters: new Filters(
        //                cuttingPlanes: new List<CuttingPlane> {
        //                new CuttingPlane(
        //                    name: "velocity-plane",
        //                    scalarField: scalarField,
        //                    center: new Vector3D(x: 0.05m, y: 0m, z: 0m),
        //                    normal: new Vector3D(x: 1m, y: 0m, z: 0m),
        //                    opacity: 1,
        //                    clipping: true,
        //                    renderMode: RenderMode.SURFACES
        //                )
        //                }
        //            ),
        //            cameraSettings: new TopViewPredefinedCameraSettings(
        //                projectionType: ProjectionType.ORTHOGONAL,
        //                directionSpecifier: TopViewPredefinedCameraSettings.DirectionSpecifierEnum.XNEGATIVE
        //            ),
        //            outputSettings: new ScreenshotOutputSettings(
        //                name: "Output 1",
        //                format: ScreenshotOutputSettings.FormatEnum.PNG,
        //                resolution: new ResolutionInfo(x: 1440, y: 1080),
        //                frameIndex: 1,
        //                showLegend: true,
        //                showCube: false
        //            )
        //        )
        //    );

        //    // Creating report
        //    Console.WriteLine("Creating report...");
        //    var createReportResponse = reportsApi.CreateReport(projectId, reportRequest);
        //    var reportId = createReportResponse.ReportId;

        //    // Start report job
        //    Console.WriteLine("Starting report with ID: " + reportId);
        //    reportsApi.StartReportJob(projectId, reportId);

        //    HashSet<ReportResponse.StatusEnum> terminalReportStatuses = new HashSet<ReportResponse.StatusEnum> {
        //    ReportResponse.StatusEnum.FINISHED,
        //        ReportResponse.StatusEnum.CANCELED,
        //        ReportResponse.StatusEnum.FAILED
        //};
        //    var report = reportsApi.GetReport(projectId, reportId);

        //    while (!terminalReportStatuses.Contains(report.Status))
        //    {
        //        Thread.Sleep(30000);
        //        report = reportsApi.GetReport(projectId, reportId);
        //        Console.WriteLine("Report generation status: " + report.Status);
        //    }

        //    using (var client = new WebClient())
        //    {
        //        Console.WriteLine("Downloading file from: " + report.Download.Url);
        //        client.Headers.Add(API_KEY_HEADER, API_KEY);
        //        client.DownloadFile(report.Download.Url, "report." + report.Download.Format);
        //    }
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return null;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("87AD7DEA-50B0-4453-BA93-429CE7229AAF"); }
        }
    }
}