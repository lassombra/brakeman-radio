using DV.Logic.Job;
using DV.ThingTypes;
using DV.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace BrakemanRadio
{
	public class NextSwitchMonitor : SingletonBehaviour<NextSwitchMonitor>
	{
		private Coroutine coroutine;
		private GeneralLicenseType_v2 license;

		public Junction NextJunction { get; private set; } = null;

		private bool HasLicense
		{
			get
			{
				return !BrakemanRadioControl.Settings.RequireLicense || LicenseManager.Instance.IsGeneralLicenseAcquired(this.license);
			}
		}

		public new static string AllowAutoCreate()
		{
			return "[Next Switch Monitor]";
		}

		public void Start()
		{
			DV.Globals.G.Types.TryGetGeneralLicense("BR_AdvancedSwitching", out this.license);
			BrakemanRadioControl.Debug("Starting NextSwitchMonitor");
			this.coroutine = StartCoroutine(MonitorCar());
			UnloadWatcher.UnloadRequested += Cleanup;
		}

		private void Cleanup()
		{
			StopCoroutine(this.coroutine);
			WorldStreamingInit.LoadingFinished += Start;
			UnloadWatcher.UnloadRequested -= Cleanup;
		}

		private IEnumerator MonitorCar()
		{
			while (true)
			{
				if (!this.HasLicense)
				{
					NextJunction = null;
				}
				else
				{
					var car = LeadingEdge;
					if (car != null)
					{
						BrakemanRadioControl.Debug("Front Car" + car);
					}
					var track = LeadingEdge?.logicCar?.CurrentTrack;
					if (track == null)
					{
						this.NextJunction = null;
					}
					else
					{
						BrakemanRadioControl.Debug("Car moving");
						var train = LeadingEdge;
						var bogies = train.Bogies[0];
						NextJunction = GetNextJunction(bogies.track, train.GetForwardSpeed(), bogies.TrackDirectionSign);
						BrakemanRadioControl.Debug("NextJunction " + NextJunction.name);
					}
				}
				yield return new WaitForSeconds(1f);
			}
		}

		private Junction GetNextJunction(RailTrack track, float? speed, float? trackDirectionSign)
		{
			BrakemanRadioControl.Debug("Getting next junction for track " + track.logicTrack.ID.FullID + " speed: " + speed + " trackDirection: " + trackDirectionSign);
			if (speed * trackDirectionSign > 0)
			{
				if (track.outJunction != null && track.outJunction.inBranch.track == track)
				{
					return track.outJunction;
				} else if (track.outJunction != null)
				{
					return GetNextJunction(track.outJunction.inBranch.track, track.outJunction);
				}
				else
				{
					return GetNextJunction(track.outBranch?.track, track);
				}
			} else
			{
				if (track.inJunction != null && track.inJunction.inBranch.track == track)
				{
					return track.inJunction;
				}
				else if (track.inJunction != null)
				{
					return GetNextJunction(track.inJunction.inBranch.track, track.inJunction);
				}
				else
				{
					return GetNextJunction(track.inBranch?.track, track);
				}
			}
		}

		private Junction GetNextJunction(RailTrack nextTrack, RailTrack lastTrack)
		{
			BrakemanRadioControl.Debug("Getting next junction walking to " + nextTrack.logicTrack.ID.FullID + " from " + lastTrack.logicTrack.ID.FullID);
			if (nextTrack == null)
			{
				return null;
			}
			if (nextTrack.inBranch?.track == lastTrack)
			{
				if (nextTrack.outJunction != null && nextTrack.outJunction.inBranch?.track == nextTrack) {
					return nextTrack.outJunction;
				} else if (nextTrack.outJunction != null)
				{
					return GetNextJunction(nextTrack.outJunction.inBranch?.track, nextTrack.outJunction);
				}
				{
					return GetNextJunction(nextTrack.outBranch?.track, nextTrack);
				}
			} else
			{
				if (nextTrack.inJunction != null && nextTrack.inJunction.inBranch?.track == nextTrack)
				{
					return nextTrack.inJunction;
				}
				else if (nextTrack.inJunction != null) {
					return GetNextJunction(nextTrack.inJunction.inBranch?.track, nextTrack.inJunction);
				}
				else
				{
					return GetNextJunction(nextTrack.inBranch?.track, nextTrack);
				}
			}
		}

		private Junction GetNextJunction(RailTrack track, Junction inJunction)
		{
			BrakemanRadioControl.Debug("Getting next junction walking to " + track.logicTrack.ID + " from " + inJunction.name);
			if (track == null)
			{
				return null;
			}
			if (track.inJunction == inJunction && track.outJunction != null)
			{
				if (track.outJunction.inBranch?.track == track)
				{
					return track.outJunction;
				} else
				{
					return GetNextJunction(track.outJunction.inBranch?.track, track.outJunction);
				}
			} else if (track.inJunction == inJunction)
			{
				return GetNextJunction(track.outBranch?.track, track);
			} else if (track.outJunction == inJunction && track.inJunction != null)
			{
				if (track.inJunction?.inBranch?.track == track)
				{
					return track.inJunction;
				} else
				{
					return GetNextJunction(track.inJunction.inBranch?.track, track.inJunction);
				}
			} else
			{
				return GetNextJunction(track.inBranch?.track, track);
			}
		}

		private TrainCar LeadingEdge { get
			{
				var trainset = PlayerManager.Car?.trainset;
				var frontCar = trainset?.firstCar;
				if (frontCar?.GetAbsSpeed() <= 0.1f)
				{
					return null;
				}
				var frontCarFrontCoupled = frontCar?.frontCoupler.IsCoupled() ?? false;
				if (frontCar != null && frontCarFrontCoupled && frontCar.GetForwardSpeed() < 0)
				{
					return frontCar;
				} else if (frontCar != null && !frontCarFrontCoupled && frontCar.GetForwardSpeed() > 0)
				{
					return frontCar;
				}
				return trainset?.lastCar;
			}
		}
	}
}
