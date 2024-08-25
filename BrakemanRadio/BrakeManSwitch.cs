using DV;
using System;
using UnityEngine;
using TMPro;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using DV.UI;
using DV.Utils;
using UnityEngine.Playables;
using DV.ThingTypes;

namespace BrakemanRadio
{
	internal class BrakeManSwitch : MonoBehaviour, ICommsRadioMode
	{
		private void Awake()
		{
			var commsRadio = this.GetComponentInParent<CommsRadioController>();
			this.lcd = commsRadio.switchControl.lcd;
			this.signalOrigin = commsRadio.switchControl.signalOrigin;
			this.display = commsRadio.switchControl.display;
			this.switcher = base.gameObject.AddComponent<CommsJunctionSwitcher>();
			this.switcher.JunctionHovered += UpdateLCD;
			this.switcher.JunctionUnHovered += UpdateLCD;
			this.switcher.JunctionSwitched += UpdateLCD;
			NextSwitchMonitor.Instance.NewJunction += UpdateLCD;
			DV.Globals.G.Types.TryGetGeneralLicense("BR_AdvancedSwitching", out this.license);
		}

		private void UpdateLCD(Junction junction)
		{
			BrakemanRadioControl.Debug("Updating LCD for junction upcoming");
			UpdateLCD();
		}

		private void Start()
		{
			this.lcd.TurnOff();
			if (!VRManager.IsVREnabled())
			{
				gameObject.AddComponent<BrakemanSwitchChecker>();
			}
			gameObject.AddComponent<BrakemanRemoteHaptics>();
		}

		private void UpdateLCD(JunctionSwitchRemoteControllable controllable)
		{
			UpdateLCD();
		}

		public ButtonBehaviourType ButtonBehaviour => ButtonBehaviourType.Regular;

		private void UpdateLCD()
		{
			BrakemanRadioControl.Debug("Updating LCD");
			if (this.switcher.PointedSwitch != null)
			{
				BrakemanRadioControl.Debug("Pointed switch is not null");
				bool isLeft = this.switcher.PointedSwitch.IsPointingLeft();
				if (this.switcher.PointedSwitch.IsBehind(base.transform))
				{
					isLeft = !isLeft;
				}
				this.lcd.TurnOn(isLeft);
				this.display.SetContent(CommsRadioLocalization.SWITCH_INSTRUCTION);
			}
			else if (this.NextSwitch != null)
			{
				BrakemanRadioControl.Debug("Next Switch is not null");
				this.lcd.TurnOn(NextSwitch.selectedBranch == 0);
				this.display.SetContent("Toggle next switch in line");
				BrakemanRadioControl.Debug("Updated for next switch");
			} else
			{
				this.lcd.TurnOff();
				this.display.SetContent(CommsRadioLocalization.SWITCH_INSTRUCTION);
			}
		}
		public bool ButtonACustomAction()
		{
			return false;
		}

		public bool ButtonBCustomAction()
		{
			return false;
		}

		public void Disable()
		{
			this.switcher.enabled = false;
			NextSwitchMonitor.Instance.Disable();
			this.lcd.TurnOff();
		}

		public void Enable()
		{
			this.switcher.enabled = true;
			NextSwitchMonitor.Instance.Enable();
		}

		public Color GetLaserBeamColor() => BrakeManSwitch.laserColor;

		public void OnUpdate()
		{
		}

		Junction NextSwitch
		{
			get
			{
				if (!LicenseManager.Instance.IsGeneralLicenseAcquired(this.license) && BrakemanRadioControl.Settings.RequireLicense)
				{
					return null;
				}
				return NextSwitchMonitor.Instance.NextJunction;
			}
		}

		public void OnUse()
		{
			var pointed = this.switcher.PointedSwitch?.VisualSwitch.junction ?? NextSwitch;
			if (pointed == null)
			{
				return;
			}
			var outTrack = pointed.outBranches[pointed.selectedBranch].track;
			var tracks = new RailTrack[] { outTrack, outTrack.inJunction == pointed ? outTrack.outBranch.track : outTrack.inBranch.track };
			var bogies = tracks.SelectMany(track => track.onTrackBogies);
			var trainsetsOnOutTrack = bogies.Select(bogie => bogie.Car.trainset);
			foreach (var trainset in trainsetsOnOutTrack.Distinct())
			{
				bool in_loaded, out_loaded;
				var carsToGo = CheckJunkctionStraddled(pointed, trainset, out in_loaded, out out_loaded);
				
				if (in_loaded && out_loaded)
				{
					SetPendingSwtich(pointed, trainset);
					return;
				}
			}
			BrakemanRadioControl.Debug("Radio used, switch isn't straddled");
			if (this.switcher.PointedSwitch == null && NextSwitch != null)
			{
				BrakemanRadioControl.Debug("Switching a distant switch");
				NextSwitch.Switch(Junction.SwitchMode.REGULAR);
				UpdateLCD();
				ClearReverseSwitchesBeyond(NextSwitch);
				return;
			}
			this.switcher.Use();
			if (this.switcher.PointedSwitch != null)
			{
				ClearReverseSwitchesBeyond(this.switcher.PointedSwitch.VisualSwitch.junction);
			}
			UpdateLCD();
		}

		private void ClearReverseSwitchesBeyond(Junction nextSwitch)
		{
			if (!LicenseManager.Instance.IsGeneralLicenseAcquired(license))
			{
				return;
			}
			foreach (var junction in Walker.WalkJunctions(nextSwitch, true))
			{
				if (junction?.junction != null && !junction.isForward && junction.isMisaligned)
				{
					junction.junction.Switch(Junction.SwitchMode.REGULAR);
				}
				if (junction?.junction != null && junction.isForward)
				{
					break;
				}
			}
		}

		private static double CheckJunkctionStraddled(Junction pointed, Trainset trainset, out bool in_loaded, out bool out_loaded)
		{
			var tracks = trainset.cars.SelectMany(car => car.Bogies)
				.Select(bogie => bogie.track)
				.Distinct();
			in_loaded = false;
			out_loaded = false;
			var inTracks = TracksFromBranch(pointed.inBranch, pointed);
			var outTracks = TracksFromBranch(pointed.outBranches[pointed.selectedBranch], pointed);
			foreach (var track in tracks)
			{
				if (inTracks.Contains(track))
				{
					in_loaded = true;
				}
				if (outTracks.Contains(track))
				{
					out_loaded = true;
				}
			}
			if (!in_loaded || !out_loaded)
			{
				return 0.0;
			}
			var Bogies = trainset.firstCar.Bogies.Concat(trainset.lastCar.Bogies).Distinct().ToList();
			var distance = WalkTrackToBogies(pointed, Bogies);
			return distance;
		}
		private static RailTrack[] TracksFromBranch(Junction.Branch branch, Junction sourceJunction)
		{
			var firstTrack = branch.track;
			if (firstTrack.inJunction == sourceJunction)
			{
				if (firstTrack.outJunction != null)
				{
					var track = NextTrackFromJunction(firstTrack.outJunction, firstTrack);
					return new RailTrack[] { track, firstTrack };
				}
				return new RailTrack[]{ firstTrack, firstTrack.outBranch.track};
			} else
			{
				if (firstTrack.inJunction != null)
				{
					var track = NextTrackFromJunction(firstTrack.inJunction, firstTrack);
					return new RailTrack[] { track, firstTrack };
				}
				return new RailTrack[] { firstTrack, firstTrack.inBranch.track };
			}
		}

		private static RailTrack NextTrackFromJunction(Junction inJunction, RailTrack firstTrack)
		{
			if (inJunction.inBranch.track == firstTrack)
			{
				return inJunction.outBranches[inJunction.selectedBranch].track;
			} else
			{
				return inJunction.inBranch.track;
			}
		}

		private static double WalkTrackToBogies(Junction junction, List<Bogie> bogies)
		{
			Dictionary<Bogie, double> map = new Dictionary<Bogie, double>();
			var outTrack = junction.outBranches[junction.selectedBranch].track;
			var found = false;
			var lastRun = false;
			var distanceSoFar = 0.0;
			foreach (var track in Walker.WalkTracks(outTrack, outTrack.inJunction == junction ? 1 : -1))
			{
				foreach (var trackedBogie in track.Key.onTrackBogies)
				{
					if (bogies.Contains(trackedBogie))
					{
						found = true;
						map[trackedBogie] = distanceSoFar + (track.Value > 0 ? trackedBogie.traveller.Span : track.Key.logicTrack.length - trackedBogie.traveller.Span);
					}
				}
				distanceSoFar += track.Key.logicTrack.length;
				BrakemanRadioControl.Debug("Walking for switch\t" + track.Key.logicTrack.ID + "\t" + distanceSoFar);
				if (lastRun)
				{
					break;
				}
				lastRun = found;
			}
			if (map.Count == 0) { return 0; }
			var bogie = map.ToDictionary(x => x.Value, y => y.Key)[map.Values.Max()];
			var car = bogie.Car;
			return map.Values.Max() / car.logicCar.length;
		}

		private void SetPendingSwtich(Junction pointed, Trainset trainset)
		{
			if (pointed != null && !monitoredJunctions.ContainsKey(pointed))
			{
				var coroutine = CoroutineManager.Instance.StartCoroutine(SwitchMonitor(pointed, trainset));
				monitoredJunctions[pointed] = coroutine;
			} else if (pointed != null)
			{
				CoroutineManager.Instance.StopCoroutine(monitoredJunctions[pointed]);
				monitoredJunctions.Remove(pointed);
			}

		}

		private IEnumerator SwitchMonitor(Junction pointed, Trainset trainset)
		{
			bool switched = false;
			GameObject notification = null;
			while (!switched)
			{
				bool in_loaded, out_loaded;
				var distance = CheckJunkctionStraddled(pointed, trainset, out in_loaded, out out_loaded);
				if (in_loaded &&  out_loaded)
				{
					string message = "";
					if (distance > 2)
					{
						message = Math.Floor(distance).ToString("F0") + " cars to go.";
					} else if (distance < 1)
					{
						message = "Almost there.";
					} else
					{
						message = "1 car to go.";
					}
					if (notification != null)
					{
						SingletonBehaviour<ACanvasController<CanvasController.ElementType>>.Instance.NotificationManager.ClearNotification(notification);
					}
					notification = SingletonBehaviour<ACanvasController<CanvasController.ElementType>>.Instance.NotificationManager.ShowNotification(message, localize: false);
					yield return new WaitForSeconds(0.5f);
				} else
				{
					switched = true;
					pointed.Switch(Junction.SwitchMode.REGULAR);
					ClearReverseSwitchesBeyond(pointed);
					SetPendingSwtich(null, null);
				}
			}
			if (notification != null)
			{
				SingletonBehaviour<ACanvasController<CanvasController.ElementType>>.Instance.NotificationManager.ClearNotification(notification);
			}
			notification = SingletonBehaviour<ACanvasController<CanvasController.ElementType>>.Instance.NotificationManager.ShowNotification("Switched", localize: false);
			yield return new WaitForSeconds(1.0f);
			SingletonBehaviour<ACanvasController<CanvasController.ElementType>>.Instance.NotificationManager.ClearNotification(notification);
		}

		public void OverrideSignalOrigin(Transform signalOrigin)
		{
			this.signalOrigin = signalOrigin;
			this.switcher.pointerOrigin = signalOrigin;
		}

		public void SetStartingDisplay()
		{
			this.display.SetDisplay(CommsRadioLocalization.MODE_SWITCH, CommsRadioLocalization.SWITCH_INSTRUCTION, "", FontStyles.UpperCase);
		}

		private ArrowLCD lcd;
		private Transform signalOrigin;
		private CommsRadioDisplay display;
		private CommsJunctionSwitcher switcher;
		private static Color laserColor = new Color(1.0f, 0f, 0f);
		private GeneralLicenseType_v2 license;
		private Dictionary<Junction, Coroutine> monitoredJunctions = new Dictionary<Junction, Coroutine>();
	}
}
