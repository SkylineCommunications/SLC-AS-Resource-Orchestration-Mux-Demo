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

15/08/2023	1.0.0.1		GMV, Skyline	Initial version based from the SRM LSO Template
***********************************************************************************************************************/

namespace SRMLSOMux
{
    using System;
    using System.Linq;
    using Newtonsoft.Json;
    using Skyline.DataMiner.Automation;
    using Skyline.DataMiner.Library.Automation;
    using Skyline.DataMiner.Library.Solutions.SRM;
    using Skyline.DataMiner.Library.Solutions.SRM.LifecycleServiceOrchestration;
    using Skyline.DataMiner.Library.Solutions.SRM.Logging;
    using Skyline.DataMiner.Library.Solutions.SRM.Logging.Orchestration;
    using Skyline.DataMiner.Library.Solutions.SRM.Model;
    using Skyline.DataMiner.Library.Solutions.SRM.Model.Events;

    /// <summary>
    /// DataMiner Script Class.
    /// </summary>
    public class Script
    {
        #region Public methods

        /// <summary>
        /// The Script entry point.
        /// </summary>
        /// <param name="engine">Link with SLScripting process.</param>
        public static void Run(Engine engine)
        {
            SrmBookingConfiguration srmBookingConfig = null;

            try
            {
                // Common part of the LSO scripts, this will parse the script input parameters and check if resource configuration should be done or not
                // Normally this should be the same in all custom LSO scripts
                var bookingManagerInfo = engine.GetScriptParamValue(
                    "Booking Manager Info",
                    rawValue => JsonConvert.DeserializeObject<BookingManagerInfo>(rawValue));
                var reservationGuid = engine.GetScriptParamValue<string>("ReservationGuid");
                var enhancedAction = new LsoEnhancedAction(engine.GetScriptParamValue<string>("Action"));

                srmBookingConfig = new SrmBookingConfiguration(
                    reservationGuid,
                    bookingManagerInfo,
                    enhancedAction.Event,
                    engine);

                if(string.Equals(
                    enhancedAction.PreviousServiceState,
                    DefaultValue.StateFailed,
                    StringComparison.InvariantCultureIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Booking {srmBookingConfig.Reservation.Name} is in Failed State, so orchestration will be done for Action: {enhancedAction}");
                }

                if(!srmBookingConfig.ShouldConfigureResources)
                {
                    srmBookingConfig.Logger
                        .BufferMessage(
                            "Booking resource configuration is disabled",
                            LogFileType.User,
                            SrmLogLevel.Warning);
                    return;
                }

                // End of common part
                engine.GenerateInformation($"DEBUG:enhancedAction {JsonConvert.SerializeObject(enhancedAction)}");
                engine.GenerateInformation($"DEBUG:reservationGuid {reservationGuid}");
                engine.GenerateInformation($"DEBUG:srmBookingConfig {JsonConvert.SerializeObject(srmBookingConfig)}");

                SimpleOrchestrationExample(enhancedAction, srmBookingConfig);
            }
            catch(Exception e)
            {
                // notify failure
                AutomationScript.HandleException(engine, e);
                throw;
            }
        }
        #endregion

        #region Private methods
        private static void SimpleOrchestrationExample(
            LsoEnhancedAction enhancedAction,
            SrmBookingConfiguration srmBookingConfig)
        {
            var resources = srmBookingConfig.GetResourcesToOrchestrate().ToArray();

            var totalResources = resources.Length;
            var configuredResources = 0;
            var nonConfiguredResources = 0;

            // Simple case where we loop through all resources in the booking,
            // If the booking is starting, i.e. event is start booking with preroll,
            // We will apply the selected profile instance/overridden parameters selected when creating the booking
            // For the other possible states we will apply the corresponding State Profile Instance
            foreach(var resource in resources)
            {
                configuredResources++;

                try
                {
                    switch(enhancedAction.Event)
                    {
                        case SrmEvent.START_BOOKING_WITH_PREROLL:
                            resource.ApplyProfile("START");
                            break;

                        case SrmEvent.STOP:
                            resource.ApplyProfile("STOP");
                            break;
                        default:
                            // nothing to configure here
                            break;
                    }
                }
                catch(Exception e)
                {
                    nonConfiguredResources++;

                    var message = $"Booking {resource.Identifier} ({configuredResources}/{totalResources}) could not be successfully configured due to:\r\n{e}";

                    srmBookingConfig.Logger.BufferMessage(message, LogFileType.Debug, SrmLogLevel.Warning);

                    message = $"Booking {resource.Identifier} ({configuredResources}/{totalResources}) could not be successfully configured because: {e.Message}";

                    srmBookingConfig.Logger.BufferActionMessage(message, LogEntryType.Critical);
                }
            }

            // Add information about the Service State that was Orchestrated and the amount of the (non)configured Resource.
            // Add as Critical when some resources weren't configure, otherwise as Normal.
            srmBookingConfig.Logger
                .BufferActionMessage(
                    $"Orchestrated service state '{enhancedAction.ServiceState}' (configured {nonConfiguredResources - configuredResources}/{configuredResources})",
                    nonConfiguredResources > 0 ? LogEntryType.Critical : LogEntryType.Normal);

            // Notify the caller script that the booking configuration has failed
            if(nonConfiguredResources > 0)
            {
                throw new SrmConfigurationException(
                    string.Format("Failed to configure {0} resources", nonConfiguredResources));
            }
        }
        #endregion
    }

}
