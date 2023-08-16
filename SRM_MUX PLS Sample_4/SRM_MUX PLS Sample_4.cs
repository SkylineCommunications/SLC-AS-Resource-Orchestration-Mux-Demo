/***********************************************************************************************************************
*  Copyright (c) 2021,  Skyline Communications NV  All Rights Reserved.												   *
************************************************************************************************************************

By using this script, you expressly agree with the usage terms and
conditions set out below.
This script and all related materials are protected by copyrights and
other intellectual property rights that exclusively belong
to Skyline Communications.

A user license granted for this script is strictly for personal use only.
This script may not be used in any way by anyone without the prior
written consent of Skyline Communications. Any sublicensing of this
script is forbidden.

Any modifications to this script by the user are only allowed for
personal use and within the intended purpose of the script,
and will remain the sole responsibility of the user.
Skyline Communications will not be responsible for any damages or
malfunctions whatsoever of the script resulting from a modification
or adaptation by the user.

The content of this script is confidential information.
The user hereby agrees to keep this confidential information strictly
secret and confidential and not to disclose or reveal it, in whole
or in part, directly or indirectly to any person, entity, organization
or administration without the prior written consent of
Skyline Communications.

Any inquiries can be addressed to:

	Skyline Communications NV
	Ambachtenstraat 33
	B-8870 Izegem
	Belgium
	Tel.	: +32 51 31 35 69
	Fax.	: +32 51 31 01 29
	E-mail	: info@skyline.be
	Web		: www.skyline.be
	Contact	: Ben Vandenberghe

************************************************************************************************************************
Revision History:

DATE		VERSION		AUTHOR			COMMENTS

11/07/2023	1.0.0.1		GMV, Skyline	Initial version
15/08/2023	2.0.0.1		GMV, Skyline	Updated to use profile instances as values instead of references.
***********************************************************************************************************************/
namespace SRMMUXPLSSample
{

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Newtonsoft.Json;
    using Skyline.DataMiner.Automation;
    using Skyline.DataMiner.Library.Exceptions;
    using Skyline.DataMiner.Library.Profile;
    using Skyline.DataMiner.Library.Solutions.SRM;
    using Skyline.DataMiner.Library.Solutions.SRM.LifecycleServiceOrchestration;
    using Skyline.DataMiner.Library.Solutions.SRM.Logging.Orchestration;
    using Skyline.DataMiner.Net;
    using Skyline.DataMiner.Net.Messages.SLDataGateway;
    using Skyline.DataMiner.Net.Profiles;
    using Skyline.DataMiner.Net.ResourceManager.Objects;

    /// <summary>
    /// Represents a DataMiner Automation script.
    /// </summary>
    public class Script
    {
        #region Public methods

        /// <summary>
        /// The script entry point.
        /// </summary>
        /// <param name="engine">Link with SLAutomation process.</param>
        public void Run(Engine engine)
        {
            // Load resource and profile node configuration.
            SrmResourceConfigurationInfo configurationInfo = LoadResourceConfigurationInfo(engine);
            NodeProfileConfiguration nodeProfileConfiguration = LoadNodeProfileConfiguration(engine);

            // A Loger can be used to set messages in a consolidated file
            var helper = new ProfileParameterEntryHelper(engine, configurationInfo?.OrchestrationLogger);
            var resource = engine.GetDummy("OutputMuxDve");

            helper.Log(
                $"PLS running with configurationInfo = {JsonConvert.SerializeObject(configurationInfo, Formatting.Indented)}",
                LogEntryType.Info);
            helper.Log(
                $"PLS running wiht nodeProfileConfiguration = {JsonConvert.SerializeObject(nodeProfileConfiguration, Formatting.Indented)}",
                LogEntryType.Info);

            // Retrieve from the resource the overriden parameter to apply, if the ProfileAction is START
            var reservation = GetReservationFromService(configurationInfo.ServiceId) ??
                throw new Exception("Couldn't retrieve the reservation from the service id");
            var resourceUsage = reservation.ResourcesInReservationInstance
                .First(r => r.GUID == configurationInfo.ResourceId) as ServiceResourceUsageDefinition;

            if(configurationInfo.ProfileAction == "START")
            {
                // We need to find the parameter by Id.
                var profileParameter = SrmManagers.ProfileManager.GetProfileParameterByName("Mux Input");
                var muxInputParameter = resourceUsage.NodeConfiguration.OverrideParameters
                    .FirstOrDefault(p => p.ParameterID == profileParameter.ID);
                resource.SetParameter(resource.GetWriteParameterIDFromRead(4003), muxInputParameter.Value.StringValue);
            }
            else
            {
                resource.SetParameter(resource.GetWriteParameterIDFromRead(4003), "NA");
            }

            helper.LogSuccess("Configuration successful");
            engine.GenerateInformation($"DEBUG:PLS completed");
        }
        #endregion

        #region Private methods
        /// <summary>
        /// Retrieves the reservation instance from the created booking service ID.
        /// </summary>
        /// <param name="serviceId">ServiceID object.</param>
        /// <returns>ReservationInstance or null.</returns>
        private static ReservationInstance GetReservationFromService(ServiceID serviceId)
        {
            var filter = ServiceReservationInstanceExposers.ServiceID.Equal(serviceId);
            var reservations = SrmManagers.ResourceManager.GetReservationInstances(filter).ToArray();
            if(reservations.Length == 0)
                return null; // ServiceReservation not found
            return reservations[0];
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
                var resourceConfiguration = JsonConvert.DeserializeObject<SrmResourceConfigurationInfo>(
                    infoPlaceHolder.Value,
                    new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Objects });
                if(resourceConfiguration == null)
                {
                    throw new ArgumentException(
                        string.Format(
                            "Could not effectively deserialize the 'Info' parameter {0}.",
                            infoPlaceHolder.Value));
                }

                return resourceConfiguration;
            }
            catch(Exception ex)
            {
                engine.GenerateInformation($"EXCEPTION:{ex.Message}");
                // Whenever an invalid or empty JSON is passed, we should support the basic flow and retrieve parameters straight from the profile instance.
                return new SrmResourceConfigurationInfo();
            }
        }
        #endregion
    }

}