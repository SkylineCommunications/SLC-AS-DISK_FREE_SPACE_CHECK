/*
****************************************************************************
*  Copyright (c) 2024,  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

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

****************************************************************************
Revision History:

DATE		VERSION		AUTHOR			COMMENTS

24/04/2024	1.0.0.1		XXX, Skyline	Initial version
****************************************************************************
*/

namespace C_Drive_Free_Space_1
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Common;
	using Skyline.DataMiner.Core.InterAppCalls.Common.CallBulk;
	using Skyline.DataMiner.Core.InterAppCalls.Common.CallSingle;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;

	/// <summary>
	/// Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		public static readonly string MicrosoftPlatformProtocol = "Microsoft Platform";

		private static readonly StringBuilder LogInput = new StringBuilder();

		private static readonly int DiskInformationTablePid = 170;

		/// <summary>
		/// The script entry point.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		public void Run(Engine engine)
		{
			try
			{
				RunSafe(engine);
			}
			catch (ScriptAbortException)
			{
				// Catch normal abort exceptions (engine.ExitFail or engine.ExitSuccess)
				throw; // Comment if it should be treated as a normal exit of the script.
			}
			catch (ScriptForceAbortException)
			{
				// Catch forced abort exceptions, caused via external maintenance messages.
				throw;
			}
			catch (ScriptTimeoutException)
			{
				// Catch timeout exceptions for when a script has been running for too long.
				throw;
			}
			catch (InteractiveUserDetachedException)
			{
				// Catch a user detaching from the interactive script by closing the window.
				// Only applicable for interactive scripts, can be removed for non-interactive scripts.
				throw;
			}
			catch (Exception e)
			{
				engine.ExitFail("Run|Something went wrong: " + e);
			}
		}

		private static void AppendLineToFinalLog(string newLine)
		{
			LogInput.AppendLine(newLine);
		}

		private void RunSafe(IEngine engine)
		{
			// DO NOT REMOVE THE COMMENTED OUT CODE BELOW OR THE SCRIPT WONT RUN!
			// Interactive scripts need to be launched differently.
			// This is determined by a simple string search looking for "engine.ShowUI" in the source code.
			// However, due to the NuGet package, this string can no longer be detected.
			// This comment is here as a temporary workaround until it has been fixed.
			//// engine.ShowUI(

			bool activeUser = engine.FindInteractiveClient("Prompt to know if script was executed by an user", 0);
			IDms thisDms = engine.GetDms();
			var activeElements = thisDms.GetElements().Where(e => e.Protocol.Name.Equals(MicrosoftPlatformProtocol) && e.State.Equals(ElementState.Active));
			var agents = thisDms.GetAgents().FirstOrDefault();
			DiskInfo diskInfo = new DiskInfo();
			List<string> elementsNames = new List<string>();
			List<int> freeDiskValues = new List<int>();
			List<int> totalSizeValues = new List<int>();
			List<float> resultValue = new List<float>();

			foreach (var element in activeElements)
			{
				AppendLineToFinalLog("Disk C Information for " + element.Name);
				elementsNames.Add(element.Name);
				var row = element.GetTable(DiskInformationTablePid).GetRow("C:");
				if (!double.TryParse(Convert.ToString(row[2]), out double totalSizeAsDouble) || !double.TryParse(Convert.ToString(row[3]), out double freeSpaceAsDouble))
				{
					AppendLineToFinalLog("No readable Information  ");

					totalSizeValues.Add(0);
					freeDiskValues.Add(0);
					resultValue.Add(0);
					continue;
				}

				// Total Size
				int totalSize = (int)Math.Floor(totalSizeAsDouble);
				totalSizeValues.Add(totalSize);
				AppendLineToFinalLog("Size in disk: " + Convert.ToString(totalSize) + " MB");

				// FreeSpace
				int freeSpace = (int)Math.Floor(freeSpaceAsDouble);
				freeDiskValues.Add(freeSpace);
				AppendLineToFinalLog("Free Space in disk: " + Convert.ToString(freeSpace) + " MB");

				// Percentages
				float percentageUtilization = (float)Math.Round((1 - ((float)freeSpace / totalSize)) * 100, 2);
				AppendLineToFinalLog("Utilization in disk: " + Convert.ToString(percentageUtilization) + " %");
				resultValue.Add(percentageUtilization);
				AppendLineToFinalLog("--------------");
			}

			diskInfo.Name = elementsNames.ToArray();
			diskInfo.FreeDisk = freeDiskValues.ToArray();
			diskInfo.Size = totalSizeValues.ToArray();
			diskInfo.ResultPercentage = resultValue.ToArray();
			if (activeUser)
			{
				var controller = new InteractiveController(engine);
				var showDiskResults = new ShowDiskSpaceResults(engine);
				controller.Run(showDiskResults);
			}
			else
			{
				LogInput.Clear();
				var healthCheckElement = thisDms.GetElements().FirstOrDefault(e => e.Protocol.Name.Equals("Skyline Health Check Manager") && e.State.Equals(ElementState.Active));
				IInterAppCall command = InterAppCallFactory.CreateNew();
				command.Messages.Add(diskInfo);
				command.Source = new Skyline.DataMiner.Core.InterAppCalls.Common.Shared.Source("C Drive Free Space");
				command.Send(Engine.SLNetRaw, healthCheckElement.AgentId, healthCheckElement.Id, Constants.InterappReceiverPid, Constants.KnownTypes);
			}
		}

		public class ShowDiskSpaceResults : Dialog
		{
			public ShowDiskSpaceResults(IEngine engine) : base(engine)
			{
				Width = 250;
				Height = 600;

				AddWidget(new Label(LogInput.ToString()), 1, 0);

				var button = new Button("Close");
				AddWidget(button, 6, 0);

				engine.Sleep(50);
				LogInput.Clear();

				button.Pressed += (sender, args) => engine.ExitSuccess("Done");
			}
		}

		public class DiskInfo : Message
		{
			public string[] Name { get; set; }

			public int[] FreeDisk { get; set; }

			public int[] Size { get; set; }

			public float[] ResultPercentage { get; set; }
		}

		public class Constants
		{
			public static readonly int InterappReceiverPid = 9000000;
			public static readonly List<Type> KnownTypes = new List<Type>
			{
				typeof(DiskInfo),
			};
		}
	}
}
