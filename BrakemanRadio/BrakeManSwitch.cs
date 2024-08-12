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
			if (this.switcher.PointedSwitch != null)
			{
				bool isLeft = this.switcher.PointedSwitch.IsPointingLeft();
				if (this.switcher.PointedSwitch.IsBehind(base.transform))
				{
					isLeft = !isLeft;
				}
				this.lcd.TurnOn(isLeft);
			}
			else
			{
				this.lcd.TurnOff();
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
		}

		public void Enable()
		{
			this.switcher.enabled = true;
		}

		public Color GetLaserBeamColor() => BrakeManSwitch.laserColor;

		public void OnUpdate()
		{
		}

		public void OnUse()
		{
			if (this.pendingSwitch != null)
			{
				return;
			}
			var pointed = this.switcher.PointedSwitch?.VisualSwitch.junction;
			if (pointed == null)
			{
				return;
			}
			var trainsetsOnOutTrack = from b in pointed.outBranches[pointed.selectedBranch].track.onTrackBogies
									  select b.Car.trainset;
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
			this.switcher.Use();
			UpdateLCD();
		}

		private static double CheckJunkctionStraddled(Junction pointed, Trainset trainset, out bool in_loaded, out bool out_loaded)
		{
			var tracks = trainset.cars.SelectMany(car => car.Bogies)
				.Select(bogie => bogie.track)
				.Distinct();
			Main.logger.Log("Tracks identified");
			foreach (var item in tracks)
			{
				Main.logger.Log("\t" + item.logicTrack.ID.FullDisplayID);
			}
			in_loaded = false;
			out_loaded = false;
			foreach (var track in tracks)
			{
				if (pointed.inBranch.track == track)
				{
					in_loaded = true;
				}
				if (pointed.outBranches[pointed.selectedBranch].track == track)
				{
					out_loaded = true;
				}
			}
			if (!in_loaded && !out_loaded)
			{
				return 0.0;
			}
			var Bogies = trainset.firstCar.Bogies.Concat(trainset.lastCar.Bogies).Distinct().ToList();
			var distance = WalkTrackToBogies(pointed, Bogies);
			Main.logger.Log("Junction status");
			Main.logger.Log("\t" + in_loaded + "\t" + out_loaded);
			Main.logger.Log("\t" + distance);
			return distance;
		}

		private static double WalkTrackToBogies(Junction junction, List<Bogie> bogies)
		{
			Dictionary<Bogie, double> map = new Dictionary<Bogie, double>();
			WalkTrackSegment(junction.outBranches[junction.selectedBranch].track, junction, ref map, bogies);
			if (map.Count == 0) { return 0; }
			var bogie = map.ToDictionary(x => x.Value, y => y.Key)[map.Values.Max()];
			var car = bogie.Car;
			return map.Values.Max() / car.logicCar.length;
		}

		private static void WalkTrackSegment(RailTrack track, Junction sourceJunction, ref Dictionary<Bogie, double> map, List<Bogie> bogies, double distanceSoFar = 0.0f)
		{
			bool lastStep = map.Count > 0;
			if (track.inJunction == sourceJunction)
			{
				foreach (var item in bogies.FindAll(bogie => bogie.track == track))
				{
					map[item] = distanceSoFar + CalculateDistanceOnTrack(track, item, true);
				}
				if (!lastStep && track.outJunction != null)
				{
					WalkTrackSegment(track.outJunction, track, ref map, bogies, distanceSoFar + track.logicTrack.length);
				}
				else if (!lastStep && track.outBranch != null)
				{
					WalkTrackSegment(track.outBranch.track, track, ref map, bogies, distanceSoFar + track.logicTrack.length);
				}
			}
			else if (track.outJunction == sourceJunction)
			{
				foreach(var item in bogies.FindAll(bogie => bogie.track == track))
				{
					map[item] = distanceSoFar + CalculateDistanceOnTrack(track, item, false);
				}
				if (!lastStep && track.inJunction != null)
				{
					WalkTrackSegment(track.inJunction, track, ref map, bogies, distanceSoFar + track.logicTrack.length);
				} else if (!lastStep && track.inBranch != null)
				{
					WalkTrackSegment(track.inBranch.track, track, ref map, bogies, distanceSoFar + track.logicTrack.length);
				}
			}
		}

		private static void WalkTrackSegment(RailTrack track, RailTrack prevTrack, ref Dictionary<Bogie, double> map, List<Bogie> bogies, double distanceSoFar)
		{
			bool lastStep = map.Count > 0;
			if (track.inBranch.track == prevTrack)
			{
				foreach (var item in bogies.FindAll(bogie => bogie.track == track))
				{
					map[item] = distanceSoFar + CalculateDistanceOnTrack(track, item, true);
				}
				if (!lastStep && track.outJunction != null)
				{
					WalkTrackSegment(track.outJunction, track, ref map, bogies, distanceSoFar + track.logicTrack.length);
				}
				else if (!lastStep && track.outBranch != null)
				{
					WalkTrackSegment(track.outBranch.track, track, ref map, bogies, distanceSoFar + track.logicTrack.length);
				}
			}
			else if (track.outBranch.track == prevTrack)
			{
				foreach (var item in bogies.FindAll(bogie => bogie.track == track))
				{
					map[item] = distanceSoFar + CalculateDistanceOnTrack(track, item, false);
				}
				if (!lastStep && track.inJunction != null)
				{
					WalkTrackSegment(track.inJunction, track, ref map, bogies, distanceSoFar + track.logicTrack.length);
				}
				else if (!lastStep && track.inBranch != null)
				{
					WalkTrackSegment(track.inBranch.track, track, ref map, bogies, distanceSoFar + track.logicTrack.length);
				}
			}
		}

		private static void WalkTrackSegment(Junction junction, RailTrack track, ref Dictionary<Bogie, double> map, List<Bogie> bogies, double distanceSoFar)
		{
			if (junction.inBranch.track == track) {
				WalkTrackSegment(junction.outBranches[junction.selectedBranch].track, junction, ref map, bogies, distanceSoFar);
			} else if (junction.outBranches[junction.selectedBranch].track == track)
			{
				WalkTrackSegment(junction.inBranch.track, junction, ref map, bogies, distanceSoFar);
			}
		}

		private static double CalculateDistanceOnTrack(RailTrack track, Bogie item, bool forward)
		{
			if (forward)
			{
				return item.traveller.Span;
			} else
			{
				return track.logicTrack.length - item.traveller.Span;
			}
		}

		private void SetPendingSwtich(Junction pointed, Trainset trainset)
		{
			this.pendingSwitch = pointed;
			if (pointed != null)
			{
				this.trainset = trainset;
				CoroutineManager.Instance.StartCoroutine(SwitchMonitor());
			} else
			{
				this.trainset= null;
			}

		}

		private IEnumerator SwitchMonitor()
		{
			bool switched = false;
			GameObject notification = null;
			while (!switched)
			{
				bool in_loaded, out_loaded;
				var distance = CheckJunkctionStraddled(pendingSwitch, trainset, out in_loaded, out out_loaded);
				if (in_loaded &&  out_loaded)
				{
					string message = "";
					if (distance > 2)
					{
						message = Math.Floor(Math.Round(distance, 0)).ToString("F0") + " cars to go.";
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
					pendingSwitch.Switch(Junction.SwitchMode.REGULAR);
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
		private Junction pendingSwitch;
		private Trainset trainset;
	}
}
