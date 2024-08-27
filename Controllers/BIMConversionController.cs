using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Xbim.Common;
using Xbim.Common.Geometry;
using Xbim.Ifc;
using Xbim.ModelGeometry.Scene;

namespace CreateWexBIM.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BIMConversionController : ControllerBase
    {
        public BIMConversionController()
        {
            Log.Logger = new LoggerConfiguration()
               .Enrich.FromLogContext()
               .WriteTo.Console()
               .CreateLogger();

            // set up xBIM logging. It will use your providers.
            XbimLogging.LoggerFactory.AddSerilog();
        }

        public class ApiResponse
        {
            public int Status { get; set; }
            public string Message { get; set; }
            public string FileName { get; set; }
            public string FileWithPath { get; set; }
        }

        [HttpPost("convert")]
        public async Task<IActionResult> ConvertToWexBim(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                var errorResponse = new ApiResponse
                {
                    Status = 400,
                    Message = "No file uploaded.",
                    FileWithPath = string.Empty
                };
                return BadRequest(errorResponse);
            }

            // Get the root directory of the project
            string rootDirectory = Directory.GetCurrentDirectory();

            // Move one level up to the parent directory
            string parentDirectory = Directory.GetParent(rootDirectory).FullName;

            // Define the specific folder in the parent directory
            string targetFolder = "ConvertedFiles";
            string targetFolderPath = Path.Combine(parentDirectory, "Xbim-react","public",targetFolder);

            // Ensure the folder exists
            Directory.CreateDirectory(targetFolderPath);

            // Define the file paths for temporary and output files in the target folder
            string tempFileName = Path.GetFileNameWithoutExtension(Path.GetTempFileName()) + ".ifc";
            string tempFilePath = Path.Combine(targetFolderPath, tempFileName);

            string wexBimFilename = string.Empty;

            try
            {
                // Save the uploaded file to the temporary file path
                using (var stream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await file.CopyToAsync(stream);
                }

                if (!System.IO.File.Exists(tempFilePath))
                {
                    var errorResponse = new ApiResponse
                    {
                        Status = 404,
                        Message = "File not found.",
                        FileWithPath = string.Empty
                    };
                    return NotFound(errorResponse);
                }

                Console.WriteLine($"File name: {tempFilePath}");
                Console.WriteLine($"File size: {new FileInfo(tempFilePath).Length / 1e6:N}MB");

                var stopwatch = Stopwatch.StartNew();
                using (var model = IfcStore.Open(tempFilePath, null, -1))
                {
                    Console.WriteLine("Creating wexBIM file from IFC model.");

                    try
                    {
                        var context = new Xbim3DModelContext(model);
                        context.CreateContext();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Exception during context creation: " + ex.Message);
                        var errorResponse = new ApiResponse
                        {
                            Status = 500,
                            Message = "Failed to create 3D context.",
                            FileWithPath = string.Empty
                        };
                        return StatusCode(500, errorResponse);
                    }

                    IVector3D translation = null;

                    // Define the file path for the wexBIM file in the target folder
                    wexBimFilename = Path.ChangeExtension(Path.Combine(targetFolderPath, Path.GetFileNameWithoutExtension(tempFileName)), "wexbim");

                    using (var wexBimFile = System.IO.File.Create(wexBimFilename))
                    using (var wexBimBinaryWriter = new BinaryWriter(wexBimFile))
                    {
                        model.SaveAsWexBim(wexBimBinaryWriter, null, translation); // Try without translation first
                        wexBimBinaryWriter.Close();
                    }

                    stopwatch.Stop();
                    Console.WriteLine($"Processing completed in {stopwatch.ElapsedMilliseconds / 1e3:N}s");
                    Console.WriteLine($"Saved file: {wexBimFilename}");

                    var successResponse = new ApiResponse
                    {
                        Status = 200,
                        Message = "File conversion successful.",
                        FileName = Path.GetFileName(wexBimFilename),
                        FileWithPath = wexBimFilename
                    };
                    return Ok(successResponse);
                }
            }
            catch (Exception ex)
            {
                var errorResponse = new ApiResponse
                {
                    Status = 500,
                    Message = $"Internal server error: {ex.Message}",
                    FileWithPath = string.Empty
                };
                return StatusCode(500, errorResponse);
            }
            finally
            {
                // Clean up temporary files
                Console.WriteLine($"Clean up temporary files: {tempFilePath}");
                Console.WriteLine($"Clean up wexBimFilename files: {wexBimFilename}");

                if (System.IO.File.Exists(tempFilePath))
                {
                    System.IO.File.Delete(tempFilePath);
                }
                // Optionally delete the wexBim file after returning the response
                // if (System.IO.File.Exists(wexBimFilename))
                // {
                //     System.IO.File.Delete(wexBimFilename);
                // }
            }
        }
    }
}
