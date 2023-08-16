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

07/07/2023	1.0.0.1		GMV, Skyline	Initial version
***********************************************************************************************************************/
namespace SRMCreateResourceOrchestrationBooking
{

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Skyline.DataMiner.Automation;
    using Skyline.DataMiner.Library.Automation;
    using Skyline.DataMiner.Library.Profile;
    using Skyline.DataMiner.Library.Solutions.SRM;
    using Skyline.DataMiner.Library.Solutions.SRM.Model;
    using Skyline.DataMiner.Library.Solutions.SRM.Model.AssignProfilesAndResources;
    using Skyline.DataMiner.Library.Solutions.SRM.Model.ReservationAction;
    using Skyline.DataMiner.Net.Messages;
    using Skyline.DataMiner.Net.Messages.SLDataGateway;
    using Skyline.DataMiner.Net.ResourceManager.Objects;
    using Parameter = Skyline.DataMiner.Library.Solutions.SRM.Model.Parameter;

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
        public void Run(IEngine engine)
        {
            try
            {
                Context context = new Context((Engine)engine);
                switch (context.RequestedAction)
                {
                    case Context.Action.Add:
                        CreateBookingWithManager((Engine)engine, context);
                        break;
                    case Context.Action.Delete:
                        DeleteBookingWithManager((Engine)engine, context);
                        break;
                    case Context.Action.Edit:
                        EditBookingWithManager((Engine)engine, context);
                        break;
                }
            }
            catch (Exception ex)
            {
                engine.GenerateInformation($"EXCEPTION:{ex.Message}");
            }
        }
        #endregion

        #region Private methods
        private static void AssignResources(IEngine engine, Context context)
        {
            var reservationId = Guid.NewGuid();
            var reservationInstance = SrmManagers.ResourceManager.GetReservationInstance(reservationId) as ServiceReservationInstance;

            var inputAssignResourceRequest = new AssignResourceRequest
            {
                TargetNodeLabel = null,
                NewResourceId = context.InputMuxResource.ID,
            };
            var outputAssignResourceRequest = new AssignResourceRequest
            {
                TargetNodeLabel = null,
                NewResourceId = context.OutputMuxResource.ID,
                ProfileInstanceId = context.InputMuxProfileInstanceId,
                ByReference = false,
            };
            var muxInputParameter = SrmManagers.ProfileManager.GetProfileParameterByName("Mux Input");
            var profileParameterInstance = new Parameter
            {
                Id = muxInputParameter.ID,
                Value = context.InputMuxResource.Name,
            };
            outputAssignResourceRequest.OverriddenParameters.Add(profileParameterInstance);
            var requests = new[] { inputAssignResourceRequest, outputAssignResourceRequest, };
            reservationInstance.AssignResources((Engine)engine, requests);
        }

        private static Function[] BuildFunctionsFromContext(Context context)
        {
            var profileParameter = SrmManagers.ProfileManager.GetProfileParameterByName("Mux Input");
            var inputMuxParameter = new Parameter { Id = profileParameter.ID, Value = context.InputMuxResource.Name, };
            List<Parameter> parameters = new List<Parameter>();
            parameters.Add(inputMuxParameter);

            // Build an array of virtual functions, assigning the resources we will use
            Function[] functions = new Function[]
            {
                new Function
                {
                    Id = 0,
                    ByReference = false,
                    SelectedResource = context.InputMuxResource.ID.ToString(),
                    IsProfileInstanceOptional = true,
                },
                new Function
                {
                    Id = 0,
                    ByReference = false,
                    SelectedResource = context.OutputMuxResource.ID.ToString(),
                    Parameters = parameters,
                },
            };
            return functions;
        }

        private static string ComposeTitle(string title)
        {
            var code = Math.Abs(Guid.NewGuid().GetHashCode()).ToString("0000").Substring(0, 4);
            return $"{title} ({code})";
        }

        private static void CreateBookingWithManager(Engine engine, Context context)
        {
            SrmCache cache = new SrmCache();
            BookingManager manager = new BookingManager("Mux Resource Orchestration");
            Booking bookingData = null;

            // Create the profile parameter for the Output Mux
            Function[] functions = BuildFunctionsFromContext(context);
            bookingData = new Booking
            {
                Recurrence =
                    new Recurrence()
                    {
                        StartDate = DateTime.SpecifyKind(context.Start, DateTimeKind.Local),
                        EndDate = DateTime.SpecifyKind(context.Stop, DateTimeKind.Local),
                    },
                Type = BookingType.SingleEvent,
                Description = ComposeTitle(context.Title),
                ConvertToContributing = false,
                ConfigureResources = true,
            };

            manager.CreateNewBooking((Engine)engine, cache, bookingData, functions);
        }

        private static void DeleteBookingWithManager(Engine engine, Context context)
        {
           // SrmCache cache = new SrmCache();
            BookingManager manager = new BookingManager("Mux Resource Orchestration");
            ReservationInstance[] reservations = GetReservationsFromContext(context);
            foreach (ReservationInstance reservation in reservations)
            {
                manager.TryDelete(engine, reservation);
            }
        }

        private static void EditBookingWithManager(Engine engine, Context context)
        {
            SrmCache cache = new SrmCache();
            BookingManager manager = new BookingManager("Mux Resource Orchestration");
            ReservationInstance reservation = GetReservationsFromContext(context).FirstOrDefault();
            if (reservation != null)
            {
                Booking bookingdata = reservation.GetBookingData();

                Function[] functiondata = reservation.GetFunctionData();
                Function[] contextFunctions = BuildFunctionsFromContext(context);
                var differences = functiondata.Where(
                        f => !contextFunctions.Any(g => g.SelectedResource == f.SelectedResource))
                        .Count() ==
                    0;
                if (differences)
                {
                    engine.GenerateInformation($"DEBUG:Replacing function resources");
                    functiondata = contextFunctions;
                }

                manager.TryEditBooking(
                    engine,
                    reservation.ID,
                    bookingdata,
                    contextFunctions,
                    null,
                    null,
                    null,
                    out reservation);
                
                if (reservation != null)
                {
                    engine.GenerateInformation($"DEBUG:Booking edited successfully");

                    // Let's check if we have a time change
                    if (reservation.Start != context.Start.ToUniversalTime() ||
                        reservation.End != context.Stop.ToUniversalTime())
                    {
                        var newtiming = new ChangeTimeInputData
                        {
                            StartDate = DateTime.SpecifyKind(context.Start, DateTimeKind.Local),
                            EndDate = DateTime.SpecifyKind(context.Stop, DateTimeKind.Local),
                            IsSilent = true,
                        };
                        if (manager.TryChangeTime(engine, ref reservation, newtiming))
                        {
                            engine.GenerateInformation($"DEBUG:Time change edited successfully");
                        }
                    }
                }
            }
        }

        private static ReservationInstance[] GetReservationsFromContext(Context context)
        {
            FilterElement<ReservationInstance> filter = ReservationInstanceExposers.Name.Contains(context.Title);
            var reservations = SrmManagers.ResourceManager.GetReservationInstances(filter);
            return reservations;
        }
        #endregion
    }

    public class Context
    {
        #region Constructors
        public Context(Engine engine)
        {
            string strStartTime = engine.GetScriptParamValue<string>("StartTime");
            string strStopTime = engine.GetScriptParamValue<string>("StopTime");
            var resources = engine.GetScriptParamValue<string>("Resources").Split(',');
            Title = engine.GetScriptParamValue<string>("Title");
            if (resources.Length != 2)
            {
                throw new ArgumentException("Invalid number of resources");
            }

            // Get the times
            Start = DateTime.Parse(strStartTime);
            Stop = DateTime.Parse(strStopTime);

            // Get the resources
            List<EligibleResourceContext> rescontext = new List<EligibleResourceContext>();
            rescontext.Add(new EligibleResourceContext(Start, Stop));
            var eligibleResources = SrmManagers.ResourceManager.GetEligibleResources(rescontext);
            InputMuxResource = eligibleResources.FirstOrDefault()?.EligibleResources.FirstOrDefault(
                    r => r.Name == resources[0].Trim()) ??
                throw new Exception($"Input mux resource {resources[0]} not found or is not available");
            OutputMuxResource = eligibleResources.FirstOrDefault()?.EligibleResources.FirstOrDefault(
                    r => r.Name == resources[1].Trim()) ??
                throw new Exception($"Output mux resource {resources[1]} not found or is not available");

            // Get the Output Mux Profile
            var inputMuxProfile = SrmManagers.ProfileManager.GetProfileInstanceByName(InputMuxResource.Name);
            InputMuxProfileInstanceId = inputMuxProfile?.ID;

            if (Enum.TryParse<Action>(engine.GetScriptParamValue<string>("Action"), out var action))
            {
                RequestedAction = action;
            }
            else
            {
                RequestedAction = Action.Add;
            }
        }
        #endregion

        #region Enumerators
        public enum Action
        {
            Add,
            Edit,
            Delete,
        }
        #endregion

        #region Public properties
        public Guid? InputMuxProfileInstanceId { get; private set; }

        public Resource InputMuxResource { get; private set; }

        public Resource OutputMuxResource { get; private set; }

        public Action RequestedAction { get; private set; }

        public DateTime Start { get; private set; }

        public DateTime Stop { get; private set; }

        public string Title { get; private set; }
        #endregion
    }

}