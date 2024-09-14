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
			UnloadWatcher.UnloadRequested -= Cleanup;
			Destroy(this);
		}

		public IEnumerator MonitorCar()
		{
			BrakemanRadioControl.Debug("Starting reverse monitor");
			while (true)
			{
				if (IsReversing())
				{
					//BrakemanRadioControl.Debug("Reversing");
					var bogie = GetLeadingBogie();
					var car = bogie.Car;
					var range = CalculateRangeToObstacle(bogie);
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

		private Bogie GetLeadingBogie()
		{
			if (this.car == null)
			{
				return null;
			}
			var relevantCar = this.car.trainset.firstCar;
			if (relevantCar.GetForwardSpeed() > 0 && relevantCar.frontCoupler.IsCoupled())
			{
				relevantCar = this.car.trainset.lastCar;
			} else if (relevantCar.GetForwardSpeed() < 0 && relevantCar.rearCoupler.IsCoupled())
			{
				relevantCar = this.car.trainset.lastCar;
			}
			if (relevantCar != null)
			{
				return GetLeadingBogie(relevantCar);
			}
			return null;
		}

		private Bogie GetLeadingBogie(TrainCar car)
		{
			var bogies = car.Bogies.OrderBy(b => b.transform.localPosition.z);
			if (car.GetForwardSpeed() > 0)
			{
				// Moving forward, so grab the highest z index bogie
				return bogies.Last();
			} else
			{
				return bogies.First();
			}
		}

		private bool CheckContact(TrainCar car)
		{
			if (!car.rearCoupler.IsCoupled() && car.rearCoupler.GetFirstCouplerInRange(0.5f) != null)
			{
				return true;
			}
			else if (!car.frontCoupler.IsCoupled() && car.frontCoupler.GetFirstCouplerInRange(0.5f) != null)
			{
				return true;
			}
			else
			{
				return false;
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
					if (range.trackName != null)
					{
						result += " until end of track " + range.trackName;
					} else
					{
						result += " until the end of available track";
					}
					break;
			}
			return result;
		}

		private RangeToObstacle CalculateRangeToObstacle(Bogie bogie)
		{
			var enumerator = Walker.WalkTracks(bogie.track, bogie.TrackDirectionSign * bogie.Car.GetForwardSpeed());
			var distanceSoFar = 0.0;
			foreach (var track in enumerator)  {
				BrakemanRadioControl.Debug("Checking Tracks for end " + track.Key.logicTrack.ID.FullID + "\t" + track.Value + "\t" + distanceSoFar);
				var directionSign = track.Value;
				var collidingBogies = GetCollidingBogies(track, bogie);
				if (collidingBogies.Count() > 0)
				{
					return CalcDistanceToBogies(track, distanceSoFar, bogie);
				} else if (AllowedTypes.Contains(track.Key.logicTrack.ID.trackType))
				{
					return CalcDistanceToEndOfTrack(track, distanceSoFar, bogie);
				}
				if (track.Key == bogie.track)
				{
					if (bogie.TrackDirectionSign * bogie.Car.GetForwardSpeed() > 0)
					{
						distanceSoFar += track.Key.logicTrack.length - bogie.traveller.Span;
					}
					else
					{
						distanceSoFar += bogie.traveller.Span;
					}
				} else
				{
					distanceSoFar += track.Key.logicTrack.length;
				}
				if (distanceSoFar > 150.0)
				{
					return null;
				}
			}
			var range = new RangeToObstacle();
			range.range = distanceSoFar;
			range.type = ObstacleType.TrackEnd;
			return range;
		}

		private IEnumerable<Bogie> GetCollidingBogies(KeyValuePair<RailTrack, float> track, Bogie bogie)
		{
			if (bogie.track == track.Key)
			{
				var directionSign = bogie.TrackDirectionSign * bogie.Car.GetForwardSpeed() > 0 ? 1 : -1;
				return from b in track.Key.onTrackBogies
					   where b.Car.trainset != bogie.Car.trainset
					   where b.traveller.Span * directionSign > bogie.traveller.Span * directionSign
					   select b;
			} else
			{
				return from b in track.Key.onTrackBogies
					   where b.Car.trainset != bogie.Car.trainset
					   select b;
			}
		}

		private RangeToObstacle CalcDistanceToEndOfTrack(KeyValuePair<RailTrack, float> track, double distanceSoFar, Bogie bogie)
		{
			var range = new RangeToObstacle();
			if (track.Key == bogie.track)
			{
				if (track.Value > 0)
				{
					range.range = track.Key.logicTrack.length - bogie.traveller.Span;
				}
				else
				{
					range.range = bogie.traveller.Span;
				}
			}
			else
			{
				range.range = track.Key.logicTrack.length + distanceSoFar;
			}
			range.type = ObstacleType.TrackEnd;
			range.trackName = track.Key.logicTrack.ID.FullDisplayID;
			return range;
		}

		private RangeToObstacle CalcDistanceToBogies(KeyValuePair<RailTrack, float> track, double distanceSoFar, Bogie bogie)
		{
			var range = new RangeToObstacle();
			if (track.Key == bogie.track)
			{
				var directionSign = bogie.TrackDirectionSign * bogie.Car.GetForwardSpeed() > 0 ? 1 : -1;
				var collidingBogie = (from b in track.Key.onTrackBogies
				 where b.Car.trainset != bogie.Car.trainset
				 where b.traveller.Span * directionSign > bogie.traveller.Span * directionSign
				 orderby b.traveller.Span * directionSign ascending
				 select b).FirstOrDefault();
				if (collidingBogie != null)
				{
					range.range = Math.Abs(collidingBogie.traveller.Span - bogie.traveller.Span);
				} else
				{
					BrakemanRadioControl.logger.Error("colliding bogie not found, but should have been");
					BrakemanRadioControl.logger.Error(track.Key.logicTrack.ID.FullID + " - " + track.Value);
				}
			} else if (track.Value > 0) 
			{
				var collidingBogie = (from b in track.Key.onTrackBogies
									  orderby b.traveller.Span ascending
									  select b).FirstOrDefault();
				if (collidingBogie != null)
				{
					range.range = distanceSoFar += collidingBogie.traveller.Span;
				}
			} else
			{
				var collidingBogie = (from b in track.Key.onTrackBogies
									  orderby b.traveller.Span descending
									  select b).FirstOrDefault();
				if (collidingBogie != null)
				{
					range.range = distanceSoFar += track.Key.logicTrack.length - collidingBogie.traveller.Span;
				}
			}
			range.type = ObstacleType.Car;
			return range;
		}

		private bool IsReversing()
		{
			if (car == null)
			{
				return false;
			}
			var velocity = car.GetAbsSpeed();
			return velocity >= 0.1f;
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
					//BrakemanRadioControl.Debug("Rear Car: " + trainset.lastCar.ID);
					return trainset.lastCar;
				}
				else if (trainset.lastCar.IsLoco)
				{
					//BrakemanRadioControl.Debug("Rear Car: " + trainset.firstCar.ID);
					return trainset.firstCar;
				}
				else if (trainset.locoIndices.Count > 0)
				{
					var walkCar = trainset.cars[trainset.locoIndices[0]];
					var lastCar = WalkCars(walkCar.rearCoupler);
					//BrakemanRadioControl.Debug("Rear Car: " + lastCar.ID);
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
