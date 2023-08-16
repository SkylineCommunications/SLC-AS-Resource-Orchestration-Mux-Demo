namespace SRMEmptyPLS
{

	using System;
	using System.Collections.Generic;
	using Newtonsoft.Json;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Library.Exceptions;
	using Skyline.DataMiner.Library.Solutions.SRM.LifecycleServiceOrchestration;

	/// <summary>
	/// Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		/// <summary>
		/// The script entry point.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		public void Run(Engine engine)
		{
			var resource = engine.GetDummy("FunctionDve");
			engine.GenerateInformation($"DEBUG:Running empty PLS over resource {resource.Name}");
		}

		/// <summary>
		/// Loads the profile instance.
		/// </summary>
		/// <param name="engine">The engine reference.</param>
		/// <returns>The <see cref="ProfileInstance"/> object.</returns>
		/// <exception cref="ArgumentException">In case there is no 'ProfileInstance' input parameter defined.</exception>
		/// <exception cref="ProfileManagerException">In case the profile instance is not found.</exception>
		private static NodeProfileConfiguration LoadNodeProfileConfiguration(Engine engine)
		{
			var instancePlaceHolder = engine.GetScriptParam("ProfileInstance");
			if(instancePlaceHolder == null)
			{
				throw new ArgumentException("There is no input parameter named Info");
			}

			try
			{
				var data = JsonConvert.DeserializeObject<Dictionary<string, Guid>>(instancePlaceHolder.Value);

				return new NodeProfileConfiguration(data);
			}
			catch(Exception ex)
			{
				throw new ArgumentException(string.Format("Invalid input parameter 'ProfileInstance': \r\n{0}", ex));
			}
		}

		/// <summary>
		/// Loads resource configuration info object.
		/// </summary>
		/// <param name="engine">The engine reference.</param>
		/// <returns>The <see cref="SrmResourceConfigurationInfo"/> object.</returns>
		/// <exception cref="ArgumentException">In case there is no 'Info' input parameter defined.</exception>
		private static SrmResourceConfigurationInfo LoadResourceConfigurationInfo(Engine engine)
		{
			var infoPlaceHolder = engine.GetScriptParam("Info");
			if(infoPlaceHolder == null)
			{
				throw new ArgumentException("There is no input parameter named Info");
			}

			try
			{
				var resourceConfiguration =
					JsonConvert.DeserializeObject<SrmResourceConfigurationInfo>(infoPlaceHolder.Value);
				if(resourceConfiguration == null)
				{
					throw new ArgumentException(
						string.Format(
							"Could not effectively deserialize the 'Info' parameter {0}.",
							infoPlaceHolder.Value));
				}

				return resourceConfiguration;
			}
			catch(Exception)
			{
				// Whenever an invalid or empty JSON is passed, we should support the basic flow and retrieve parameters straight from the profile instance.
				return new SrmResourceConfigurationInfo();
			}
		}
	}

}