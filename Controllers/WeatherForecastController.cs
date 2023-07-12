using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Elastic.Apm;
using Elastic.Apm.Api;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly ILogger<WeatherForecastController> _logger;

        public WeatherForecastController(ILogger<WeatherForecastController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public IEnumerable<WeatherForecast> Get()
        {
            var rng = new Random();
            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateTime.Now.AddDays(index),
                TemperatureC = rng.Next(-20, 55),
                Summary = Summaries[rng.Next(Summaries.Length)]
            })
            .ToArray();
        }

        [HttpGet("test")]
        public string Test()
        {
            var transaction = Agent.Tracer.CurrentTransaction;

            if (transaction.TryGetLabel<int>("MarkTestLabel", out var testLabel))
                Console.WriteLine(testLabel);
            
            testLabel += 1;
            transaction.SetLabel("MarkTestLabel", testLabel);
            transaction.SetLabel("MarkTestLabel2", ApmCounters.myTestCounter++);

            string outgoingDistributedTracingData = "no transaction";

            var transaction2 = Agent.Tracer.StartTransaction("DistributedTrans", ApiConstants.TypeRequest);

            try
            {
                transaction2.SetLabel("MarkTestTransLabel", ApmCounters.testTransCounter++);
                outgoingDistributedTracingData = transaction2.OutgoingDistributedTracingData?.SerializeToString();
                transaction2.CaptureSpan("EncProcessed", ApiConstants.TypeDb, (s) =>
                {
                    //execute db query
                    transaction2.SetLabel("MarkTestLabel2", ApmCounters.myTestCounter++);
                }, ApiConstants.SubtypeMssql, ApiConstants.ActionQuery);
            }
            catch (Exception ee)
            {
                transaction2.CaptureException(ee);
            }
            transaction2.End();

            return outgoingDistributedTracingData;
        }

        [HttpGet("test2/{serializedDistributedTracingData}")]
        public string Test2(string serializedDistributedTracingData)
        {
            var transaction2 = Agent.Tracer.StartTransaction("DistributedTrans", "Test1",
                                    DistributedTracingData.TryDeserializeFromString(serializedDistributedTracingData));
            transaction2.CaptureSpan("EncDownloaded", ApiConstants.TypeDb, (s) =>
            {
                //execute db query
                transaction2.SetLabel("MarkTestLabel2", ApmCounters.myTestCounter++);
            }, ApiConstants.SubtypeMssql, ApiConstants.ActionQuery);
            transaction2.End();
            return "ok";
        }

    }
}
