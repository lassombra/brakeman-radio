using DV.Logic.Job;
using DV.UI;
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
	public class TrainReverseMonitor : SingletonBehaviour<TrainReverseMonitor>
	{
		public readonly static string[] AllowedTypes = new string[]
		{
			TrackID.STORAGE_TYPE,
			TrackID.REGULAR_OUT_TYPE,
			TrackID.REGULAR_IN_TYPE,
			TrackID.LOADING_TYPE,
			TrackID.PARKING_TYPE,
			TrackID.LOADING_PASSENGER_TYPE,
			TrackID.STORAGE_PASSENGER_TYPE
		};
		public new static string AllowAutoCreate()
		{
			return "[Train Backup Distance Monitor]";
		}
		public TrainCar Car
		{
			get { return car; }
			set
			{
				if (car != value)
				{
					car = value;
					this.ClearMessage();
				}

			}
		}
		public void Start()
		{
			this.coroutine = StartCoroutine(MonitorCar());
			UnloadWatcher.UnloadRequested += Cleanup;
		}

		private void Cleanup()
		{
			StopCoroutine(this.coroutine);
			WorldStreamingInit.LoadingFinished += Start;
			UnloadWatcher.UnloadRequested -= Cleanup;
		}

		public IEnumerator MonitorCar()
		{
			while (true)
			{
				if (IsReversing())
				{
					BrakemanRadioControl.Debug("Reversing");
					var car = RearCar;
					var ranges = new List<RangeToObstacle>();
					foreach (var bogie in car.Bogies)
					{
						ranges.Add(CalculateRangeToObstacle(bogie));
					}
					var range = (from r in ranges
								 where r is not null
								 orderby r.range ascending
								 select r).FirstOrDefault();
					if (range != null)
					{
						bool collided = false;
						if (range.range < car.logicCar.length)
						{
							collided = CheckContact(car);
						}
						message = SingletonBehaviour<ACanvasController<CanvasController.ElementType>>.Instance.NotificationManager.ShowNotification(MessageForRange(range, car, collided), localize: false);
						if (range.range / car.logicCar.length < 1.0)
						{
							yield return new WaitForSeconds(0.1f);
						}
						else
						{
							yield return new WaitForSeconds(1f);
						}
					} else
					{
						if (message != null)
						{
							SingletonBehaviour<ACanvasController<CanvasController.ElementType>>.Instance.NotificationManager.ClearNotification(message);
							message = null;
						}
						yield return new WaitForSeconds(1f);
					}
				}
				else if (message != null)
				{
					SingletonBehaviour<ACanvasController<CanvasController.ElementType>>.Instance.NotificationManager.ClearNotification(message);
					message = null;
					yield return new WaitForSeconds(1f);
				}
				else
				{
					yield return new WaitForSeconds(1f);
				}
			}
		}

		private bool CheckContact(TrainCar car)
		{
			if (car.frontCoupler.IsCoupled())
			{
				return car.rearCoupler.GetFirstCouplerInRange(0.5f) != null;
			}
			else
			{
				return car.frontCoupler.GetFirstCouplerInRange(0.5f) != null;
			}
		}

		private string MessageForRange(RangeToObstacle range, TrainCar car, bool collided)
		{
			if (range.type == ObstacleType.Car && collided)
			{
				return "Contact";
			}
			var cars = Math.Floor(range.range / car.logicCar.length);
			string result = cars.ToString("F0");
			if (cars >= 2)
			{
				result += " cars";
			} else if (cars >= 1)
			{
				result += " car";
			} else
			{
				result = "less than 1 car";
			}
			switch (range.type) {
				case ObstacleType.Car:
					result += " until next car";
					break;
				case ObstacleType.TrackEnd:
					result += " until end of track " + range.trackName;
					break;
			}
			return result;
		}

		private RangeToObstacle CalculateRangeToObstacle(Bogie bogie)
		{
			var track = bogie.track;
			return WalkTrack(track, bogie);
		}

		private RangeToObstacle WalkTrack(RailTrack track, Bogie bogie)
		{
			var trackDirection = bogie.TrackDirectionSign * bogie.Car.GetForwardSpeed();
			if (trackDirection > 0)
			{
				BrakemanRadioControl.Debug("Walking forward");
				return WalkTrack(track, bogie, 1);
			} else if (trackDirection < 0)
			{
				BrakemanRadioControl.Debug("Walking backward");
				return WalkTrack(track, bogie, -1);
			}
			return null;
		}

		private RangeToObstacle WalkTrack(RailTrack track, Bogie bogie, int direction, double distanceSoFar = 0.0)
		{
			BrakemanRadioControl.Debug("walking track " + track.logicTrack.ID.FullDisplayID);
			var inRangeBogies = from b in track.onTrackBogies
								where b.Car.trainset != bogie.Car.trainset
								where bogie.track != track || ((b.traveller.Span * direction) > (bogie.traveller.Span * direction))
								select b;
			BrakemanRadioControl.Debug("Found " + inRangeBogies.Count() + " Bogies");
			if (inRangeBogies.Any())
			{
				var ret = new RangeToObstacle();
				ret.type = ObstacleType.Car;
				ret.range = inRangeBogies.Select(b => b.traveller.Span * direction).Min();
				if (track == bogie.track)
				{
					ret.range -= bogie.traveller.Span * direction;
				}
				ret.range = Math.Abs(ret.range);
				ret.range += distanceSoFar;
				return ret;
			}
			if (AllowedTypes.Contains(track.logicTrack.ID.trackType))
			{
				var ret = new RangeToObstacle();
				ret.type = ObstacleType.TrackEnd;
				ret.trackName = track.logicTrack.ID.FullDisplayID.ToString();
				ret.range = RemainingTrackLength(track, bogie, direction) + distanceSoFar;
				return ret;
			}
			var distance = distanceSoFar + RemainingTrackLength(track, bogie, direction);
			if (distance < bogie.Car.logicCar.length * 10)
			{
				return WalkTrack(GetNextTrack(track, direction), bogie, GetNextTrackDirection(track, direction), distance);
			}
			return null;
		}
		private int GetNextTrackDirection(RailTrack track, int direction)
		{
			var nextTrack = GetNextTrack(track, direction);
			if (nextTrack.inBranch.track == track)
			{
				return 1;
			} else if (
				nextTrack.outBranch.track == track)
			{
				return -1;
			} else
			{
				var junction = direction > 0 ? track.outJunction : track.inJunction;
				if (nextTrack.inJunction == junction)
				{
					return 1;
				} else
				{
					return -1;
				}
			}
		}

		private RailTrack GetNextTrack(RailTrack track, int direction)
		{
			if (direction > 0)
			{
				// we're going ot OUT side
				if (track.outJunction != null)
				{
					return GetNextTrack(track.outJunction, track);
				}
				return track.outBranch.track;
			}
			else
			{
				if (track.inJunction != null)
				{
					return GetNextTrack(track.inJunction, track);
				}
				return track.inBranch.track;
			}
		}

		private RailTrack GetNextTrack(Junction junction, RailTrack track)
		{
			if (junction.inBranch.track == track)
			{
				return junction.outBranches[junction.selectedBranch].track;
			}
			return junction.inBranch.track;
		}

		private static double RemainingTrackLength(RailTrack track, Bogie bogie, int direction)
		{
			if (bogie.track != track)
			{
				return track.logicTrack.length;
			}
			if (direction > 0)
			{
				return track.logicTrack.length - bogie.traveller.Span;
			}
			else
			{
				return bogie.traveller.Span;
			}
		}

		private bool IsReversing()
		{
			var car = RearCar;
			if (car == null)
			{
				return false;
			}
			var velocity = car.GetForwardSpeed();
			if (car.rearCoupler.IsCoupled())
			{
				velocity = -1 * velocity;
			}
			return velocity <= -0.1f;
		}

		private TrainCar RearCar
		{
			get
			{
				if (Car == null)
				{
					return null;
				}
				var trainset = car.trainset;
				if (trainset.firstCar.IsLoco)
				{
					BrakemanRadioControl.Debug("Rear Car: " + trainset.lastCar.ID);
					return trainset.lastCar;
				}
				else if (trainset.lastCar.IsLoco)
				{
					BrakemanRadioControl.Debug("Rear Car: " + trainset.firstCar.ID);
					return trainset.firstCar;
				}
				else if (trainset.locoIndices.Count > 0)
				{
					var walkCar = trainset.cars[trainset.locoIndices[0]];
					var lastCar = WalkCars(walkCar.rearCoupler);
					BrakemanRadioControl.Debug("Rear Car: " + lastCar.ID);
					return lastCar;
				}
				return null;
			}
		}

		private TrainCar WalkCars(Coupler coupler)
		{
			var prevCoupler = coupler;
			while (prevCoupler.IsCoupled())
			{
				var car = prevCoupler.coupledTo.train;
				prevCoupler = prevCoupler.coupledTo;
				prevCoupler = car.frontCoupler == prevCoupler ? car.rearCoupler : car.frontCoupler;
			}
			return prevCoupler.train;
		}

		private void ClearMessage()
		{
			if (this.message != null)
			{
				SingletonBehaviour<ACanvasController<CanvasController.ElementType>>.Instance.NotificationManager.ClearNotification(message);
			}
		}

		private TrainCar car;
		private GameObject message;
		private Coroutine coroutine;

		class RangeToObstacle
		{
			public double range;
			public ObstacleType type;
			public string trackName;
		}
		enum ObstacleType
		{
			TrackEnd,
			Car
		}
	}
}
