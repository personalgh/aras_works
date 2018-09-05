// This method perform validation of Change item(ECN, ECO, etc..) which situated in state or promoted to another state. 
// Also is performed processing of Affected Items which existing in current Change Item.
//
// Following classes encapsulate validation and processing behavior:
// ECN, SimpleECO, ExpressECO, ExpressDCO
// Validation logic is situated in ValidateState and ValidateTransition methods which implemented for each of ChangeItem inheritors.
// Process promotion is situated in ProcessTransition method.
//
// If you want to change existing behavior, you have to change ValidateState,ValidateTransition and ProcessTransition for particular class of change item(ECN, SimpleECO, ExpressECO, ExpressDCO)

    Aras.Server.Security.Identity plmIdentity = Aras.Server.Security.Identity.GetByName("Aras PLM");
    bool PermissionWasSet = Aras.Server.Security.Permissions.GrantIdentity(plmIdentity);
  
    try
    {
      Utils.InitializeForRequest(this.getInnovator(), CCO);
      ChangeItem changeItem;
      
      // // is used because possible case when Change Item is not a root item. It happens when it child of Method item.
      Item item = this.getItemsByXPath("//Item[@type='ECN' or @type='Simple ECO' or @type='Express ECO' or @type='Express DCO']");

      //Create instance of promoted item
      switch (item.getType())
      {
        case "ECN":
          changeItem = new ECN(item);
          break;
        case "Simple ECO":
          changeItem = new SimpleECO(item);
          break;
        case "Express ECO":
          changeItem = new ExpressECO(item);
          break;
        case "Express DCO":
          changeItem = new ExpressDCO(item);
          break;
        default:
          changeItem = null;
          break;
      }

      bool isProcessTransition = (changeItem.Transition != null);

      ResultStatus status = changeItem.Validate();

      if (status.IsSuccess)
      {
        if (isProcessTransition)
        {
          return changeItem.ProcessTransition();
        }
        else
        {
          return Utils.GetOkResult();
        }
      }
      else
      {
        int numberOfDisplayedErrors = isProcessTransition ? 1 : status.Errors.Count;
        return Utils.GetError(status, numberOfDisplayedErrors);
      }
    }
    finally
    {
      if (PermissionWasSet) Aras.Server.Security.Permissions.RevokeIdentity(plmIdentity);
    }
  }
}
/// <summary>
/// Interface for validate Change Item instance(ECN,Express ECO, Express DCO etc)
/// </summary>
interface IChangeItemValidationRules
{
	void IsItemExist(ResultStatus status);
	void IsAffectedItemExists(ResultStatus status);
	void IsAffectedItemOtherThanNoneActionExists(ResultStatus status);
}

/// <summary>
/// Interface for validate Affected item instance
/// </summary>
interface IAffectedItemValidationRules
{
	void Compare_AffectedId_and_NewItem_IdTypes(ResultStatus status);
	void Is_AffectedId_and_NewItemId_NotLocked(ResultStatus status);
	void Is_AffectedId_InReleasedState(ResultStatus status);
	void Is_NewItemId_InPreliminaryState(ResultStatus status);
	void Is_AffectedId_Requiered(ResultStatus status);
	void Is_NewItemId_Required(ResultStatus status);
	void Is_NewItemId_RequiresNull(ResultStatus status);
	void Is_AffectedId_RequiresNull(ResultStatus status);
	void Is_NewItemId_HasNoPreviouslyReleasedGeneration(ResultStatus status);
}

/// <summary>
/// Interface extends validation rules of Affected Item interface for validating Extednded Affected Item
/// </summary>
interface IExtendedAffectedItemValidationRules : IAffectedItemValidationRules
{
	void Is_AffectedItem_Action_Review(ResultStatus status);
	void Is_NewItemId_InReleasedState(ResultStatus status);
	void Is_AffectedId_InSupersededOrReleasedState(ResultStatus status);
	void Is_AffectedRelationship_Allowed(ResultStatus status);
	void VerifyAffectedItemProperties(ResultStatus status);
}

/// <summary>
/// Iterface for validate Affected Relationship
/// </summary>
interface IAffectedRelationshipValidationRules
{
	void Is_AffectedRelationship_RequiresAttachAction(ResultStatus status);
	void Is_AffectedRelId_Required(ResultStatus status);
	void Is_AffectedRelId_Required_AffectedId_SourceID(ResultStatus status);
	void Is_AffectedRelationshipProperty_Required(ResultStatus status);
	void Verify(ResultStatus status);
}

//  Class Diagramm:
//  abstract class ItemContext;
//  abstract class ChangeItem : ItemContext;
//
//  ->The following classes are implements own Validation and ProcessTransition behavior<-
//
//  class ECN : ChangeItem; 
//  class SimpleECO : ChangeItem;
//  class ExpressECO : ChangeItem;
//  class ExpressDCO : ChangeItem;

/// <summary>
/// Represent base class for Item wrappers
/// </summary>
abstract class ItemContext
{
	/// <summary>
	/// Constructor. Create empty prototype.
	/// </summary>
	protected ItemContext()
	{
	}

	/// <summary>
	/// Constructor. Wrap item and create Utils
	/// </summary>
	/// <param name="item"></param>
	public ItemContext(Item item)
	{
		this.Item = item;
	}

	Item p_item;
	/// <summary>
	/// Get wrapped item
	/// </summary>
	public Item Item
	{
		get { return p_item; }
		set { p_item = value; }
	}

	public string ID
	{
		get { return this.Item.getID(); }
	}

	public string Type
	{
		get { return this.Item.getType(); }
	}

	public bool IsLocked
	{
		get { return this.Item.getLockStatus() != 0; }
	}

	public string State
	{
		get { return this.Item.getProperty("state", ""); }
	}

	public string ConfigID
	{
		get { return this.Item.getProperty("config_id", ""); }
	}

	/// <summary>
	/// Promote item
	/// </summary>
	/// <param name="nextStatus">to state</param>
	/// <exception cref="ItemErrorException">throws if error</exception>
	public void Promote(string nextStatus)
	{
		Item tempItem = Utils.Innovator.newItem(this.Type);
		tempItem.setID(this.ID);
		Item res = tempItem.promote(nextStatus, "PromoteItem Affected item");

		Utils.AssertItem(res);
	}

	/// <summary>
	/// Version item
	/// </summary>
	/// <returns>versioned item</returns>
	/// <exception cref="ItemErrorException">throws if error</exception>
	public ChangeControlledItem Version()
	{
		Item tempItem = Utils.Innovator.newItem(this.Type, "version");
		tempItem.setID(this.ID);
		tempItem.setProperty("effective_date", "");
		tempItem = tempItem.apply();

		Utils.AssertItem(tempItem);

		return new ChangeControlledItem(tempItem);
	}

	/// <summary>
	/// Unlock item
	/// </summary>
	/// <exception cref="ItemErrorException">throws if error</exception>
	public void Unlock()
	{
		Item tempItem = Utils.Innovator.newItem(this.Type, "unlock");
		tempItem.setID(this.ID);
		tempItem = tempItem.apply();

		Utils.AssertItem(tempItem);
	}

	/// <summary>
	/// Apply item with action edit
	/// </summary>
	/// <exception cref="ItemErrorException">throws if error</exception>
	public void ApplyEdit()
	{
		Item tmpItem = Utils.Innovator.newItem();
		tmpItem.loadAML(this.Item.node.OuterXml);

		tmpItem.setAction("edit");
		tmpItem.setAttribute("version", "0");
		Item res = tmpItem.apply();

		Utils.AssertItem(res);
	}

	/// <summary>
	/// Copy item
	/// </summary>
	/// <typeparam name="TCopy">ItemContext inheritor</typeparam>
	/// <returns></returns>
	/// <exception cref="ItemErrorException">throws if error</exception>
	public TCopy ApplyCopy<TCopy>()
		where TCopy : ItemContext
	{
		Item tmpItem = Utils.Innovator.newItem(this.Type, "copyAsNew");
		tmpItem.setID(this.ID);
		Item res = tmpItem.apply();

		Utils.AssertItem(res);

		return (TCopy)Activator.CreateInstance(typeof(TCopy), res);
	}

	/// <summary>
	/// Apply item with action add
	/// </summary>
	/// <exception cref="ItemErrorException">throws if error</exception>
	public void ApplyAdd()
	{
		Item tmpItem = Utils.Innovator.newItem();
		tmpItem.loadAML(this.Item.node.OuterXml);

		tmpItem.setAction("add");
		tmpItem.setAttribute("version", "0");
		Item res = tmpItem.apply();

		Utils.AssertItem(res);

		this.Item = res;
	}

	/// <summary>
	/// Rised after item delete
	/// </summary>
	public event EventHandler Deleted;
	protected void FireDeleted()
	{
		if (Deleted != null)
		{
			Deleted(this, null);
		}
	}

	/// <summary>
	/// Delete item
	/// </summary>
	/// <exception cref="ItemErrorException">throws if error</exception>
	public virtual void Delete()
	{
		Item tmpItem = Utils.Innovator.newItem();
		tmpItem.loadAML(this.Item.node.OuterXml);

		tmpItem.setAction("delete");
		tmpItem.setAttribute("version", "0");
		Item res = tmpItem.apply();

		Utils.AssertItem(res);

		FireDeleted();
	}
}

/// <summary>
/// Base class for change items which has Affected item relationship
/// </summary>
abstract class ChangeItem : ItemContext, IChangeItemValidationRules
{
	protected delegate void TransitionHandler(IList<AffectedItem> item);
	protected string AffectedItemRelshipName;

	protected ChangeItem(Item changeItem, String affectedItemRelshipName)
		: base(null)
	{
		this.AffectedItemRelshipName = affectedItemRelshipName;

		Item transitionItem = changeItem.getPropertyItem("transition");
		if (transitionItem != null)
		{
			this.Transition = new LifeCycleTransition(transitionItem);
		}

		this.Item = changeItem;
	}

	#region Relationships
	RelationshipItemList<AffectedItem> p_aff_list;
	/// <summary>
	/// List of Affected Item
	/// </summary>
	public virtual RelationshipItemList<AffectedItem> AffectedItems
	{
		get
		{
			if (p_aff_list == null)
			{
				p_aff_list = new RelationshipItemList<AffectedItem>(this.AffectedItemRelshipName, new AffectedItem(), this);
			}
			return p_aff_list;
		}
	}
	#endregion

	#region Mapped Properties
	public string Number
	{
		get { return this.Item.getProperty("item_number", ""); }
	}
	#endregion

	/// <summary>
	/// Get LifeCycleTransition object
	/// </summary>
	public LifeCycleTransition Transition
	{
		get;
		private set;
	}

	/// <summary>
	/// Validate item
	/// </summary>
	/// <returns></returns>
	public ResultStatus Validate()
	{
		try
		{
			if (this.Transition != null)
			{
				return this.ValidateTransition();
			}
			else
			{
				return this.ValidateState();
			}
		}
		catch (ValidationException ex)
		{
			return ex.ResultStatus;
		}
	}

	/// <summary>
	/// Validate state item(not on promote)
	/// </summary>
	/// <returns></returns>
	/// <exception cref="ItemErrorException">throw if error</exception>
	protected virtual ResultStatus ValidateState()
	{
		//throwExceptionOnSecondError = false
		ResultStatus status = new ResultStatus(false);
		IChangeItemValidationRules thisValidationRules = (IChangeItemValidationRules)this;

		thisValidationRules.IsItemExist(status);

		return status;
	}

	/// <summary>
	/// Validate item on promote
	/// </summary>
	/// <returns></returns>
	/// <exception cref="ItemErrorException">throw if error</exception>
	protected virtual ResultStatus ValidateTransition()
	{
		//throwExceptionOnSecondError = true
		ResultStatus status = new ResultStatus(true);
		IChangeItemValidationRules thisValidationRules = (IChangeItemValidationRules)this;

		thisValidationRules.IsItemExist(status);

		return status;
	}

	/// <summary>
	/// Process item on post promote.
	/// </summary>
	/// <returns></returns>
	public virtual Item ProcessTransition()
	{
		return null;
	}

	/// <summary>
	/// Process each affected item by TransitionHandler
	/// </summary>
	/// <param name="handler">Handler which will be executed for each affected item in collection</param>
	/// <returns></returns>
	protected Item ProcessTransitionHandler(TransitionHandler handler)
	{
		if (handler != null)
		{
			try
			{
				handler(this.AffectedItems);
			}
			catch (ItemErrorException ex)
			{
				return ex.Error;
			}
		}

		return Utils.GetOkResult();
	}

	#region Validation Rules
	/// <summary>
	/// Validate that item exists in DB
	/// </summary>
	/// <param name="status"></param>
	void IChangeItemValidationRules.IsItemExist(ResultStatus status)
	{
		Item item = Utils.Innovator.getItemById(this.Type, this.ID);
		if (item == null || item.isError())
		{
			status.AddError("ValidateChangeItem_IsItemExist");
		}
	}

	/// <summary>
	/// At least one Affected Item must be attached to the change item
	/// </summary>
	/// <param name="status"></param>
	void IChangeItemValidationRules.IsAffectedItemExists(ResultStatus status)
	{
		if (this.AffectedItems.Count == 0)
		{
			status.AddError("ValidateChangeItem_IsAffectedItemExists", this.Type, this.Number);
		}
	}

	void IChangeItemValidationRules.IsAffectedItemOtherThanNoneActionExists(ResultStatus status)
	{
		IsAffectedItemOtherThanNoneActionExists(status);
	}

	protected virtual void IsAffectedItemOtherThanNoneActionExists(ResultStatus status)
	{
		if (this.AffectedItems.Count > 0 && !this.AffectedItems.Any(affectedItem => affectedItem.Action != AffectedItem.ActionType.None))
		{
			status.AddError("ValidateChangeItem_IsAffectedItemWithActionOtherThanEmptyExists", this.Type, this.Number);
		}
	}
	#endregion
}

/// <summary>
/// Represent inheritor of ChangeItem
/// </summary>
class ECN : ChangeItem
{
	public ECN(Item item)
		: base(item, "ECN Affected Item")
	{
	}

	/// <summary>
	/// Validate state item(not on promote)
	/// </summary>
	/// <returns></returns>
	/// <exception cref="ItemErrorException">throw if error</exception>
	protected override ResultStatus ValidateState()
	{
		ResultStatus status = base.ValidateState();
		if (!status.IsSuccess) return status;
		IChangeItemValidationRules thisValidationRules = (IChangeItemValidationRules)this;

		switch (this.State)
		{
			case "In Planning":
				thisValidationRules.IsAffectedItemExists(status);
				thisValidationRules.IsAffectedItemOtherThanNoneActionExists(status);

				foreach (IAffectedItemValidationRules affectedItem in this.AffectedItems)
				{
					affectedItem.Compare_AffectedId_and_NewItem_IdTypes(status);
					affectedItem.Is_AffectedId_and_NewItemId_NotLocked(status);
					affectedItem.Is_AffectedId_InReleasedState(status);
					affectedItem.Is_NewItemId_InPreliminaryState(status);
					affectedItem.Is_AffectedId_Requiered(status);
					affectedItem.Is_NewItemId_Required(status);
					affectedItem.Is_NewItemId_RequiresNull(status);
					affectedItem.Is_AffectedId_RequiresNull(status);
					affectedItem.Is_NewItemId_HasNoPreviouslyReleasedGeneration(status);
				}

				break;
			case "In Work":
			case "In Review":
				thisValidationRules.IsAffectedItemExists(status);
				thisValidationRules.IsAffectedItemOtherThanNoneActionExists(status);

				foreach (IAffectedItemValidationRules affectedItem in this.AffectedItems)
				{
					affectedItem.Compare_AffectedId_and_NewItem_IdTypes(status);
				}
				break;
		}

		return status;
	}

	/// <summary>
	/// Validate item on promote
	/// </summary>
	/// <returns></returns>
	/// <exception cref="ItemErrorException">throw if error</exception>
	protected override ResultStatus ValidateTransition()
	{
		ResultStatus status = base.ValidateTransition();
		if (!status.IsSuccess) return status;

		IChangeItemValidationRules thisValidationRules = (IChangeItemValidationRules)this;

		switch (this.Transition.ToString())
		{
			case "In Planning->In Work":
				thisValidationRules.IsAffectedItemExists(status);
				thisValidationRules.IsAffectedItemOtherThanNoneActionExists(status);

				foreach (IAffectedItemValidationRules affectedItem in this.AffectedItems)
				{
					affectedItem.Compare_AffectedId_and_NewItem_IdTypes(status);
					affectedItem.Is_AffectedId_and_NewItemId_NotLocked(status);
					affectedItem.Is_AffectedId_InReleasedState(status);
					affectedItem.Is_NewItemId_InPreliminaryState(status);
					affectedItem.Is_AffectedId_Requiered(status);
					affectedItem.Is_NewItemId_Required(status);
					affectedItem.Is_NewItemId_RequiresNull(status);
					affectedItem.Is_AffectedId_RequiresNull(status);
					affectedItem.Is_NewItemId_HasNoPreviouslyReleasedGeneration(status);
				}
				break;
			case "In Work->In Review":
			case "In Review->Released":
				thisValidationRules.IsAffectedItemExists(status);
				thisValidationRules.IsAffectedItemOtherThanNoneActionExists(status);

				foreach (IAffectedItemValidationRules affectedItem in this.AffectedItems)
				{
					affectedItem.Compare_AffectedId_and_NewItem_IdTypes(status);
				}
				break;
		}

		return status;
	}

	/// <summary>
	/// Process each affected item by TransitionHandler
	/// </summary>
	/// <param name="handler">Handler which will be executed for each affected item in collection</param>
	/// <returns></returns>
	public override Item ProcessTransition()
	{
		TransitionHandler handler = null;

		switch (this.Transition.ToString())
		{
			case "In Planning->In Work":
				IList<AffectedItem> sortedAffectedItemList = this.AffectedItems;

				#region Promote all not null affectedIdItem to In Change
				{
					IList<ChangeControlledItem> affectedIdItemList =
						(from affectedItem in sortedAffectedItemList
							where affectedItem.AffectedIdItem != null
							select affectedItem.AffectedIdItem)
						.ToList();

					affectedIdItemList.Promote("In Change");
				}
				#endregion

				#region Version affectedIdItem with action Change and Interchangeable = true
				{
					IList<AffectedItem> affectedItemList =
					(from affectedItem in sortedAffectedItemList
						where
						affectedItem.Action == AffectedItem.ActionType.Change &&
						affectedItem.Interchangeable
						select affectedItem)
					.ToList();

					IList<ChangeControlledItem> versionedAffectedIdItemList =
						affectedItemList
						.Select(affectedItem => affectedItem.AffectedIdItem)
						.ToList()
						.Version();

					versionedAffectedIdItemList.Unlock();

					affectedItemList = affectedItemList.Select((AffectedItem affectedItem, int index) =>
					{
						affectedItem.NewItemIdItem = versionedAffectedIdItemList[index];
						return affectedItem;
					}
					)
					.ToList();

					affectedItemList.Lock();
					affectedItemList.ApplyUpdate("new_item_id");
					affectedItemList.Unlock();
				}
				#endregion
				break;
			case "In Work->In Review":
				handler = InWork_InReview;
				break;
			//+++ Added for IR-016676
			case "In Review->In Work":
				handler = InReview_InWork;
				break;
			//--- Added for IR-016676
			case "In Review->Released":
				handler = InReview_Released;
				break;
		}

		return ProcessTransitionHandler(handler);
	}

	#region Dispatch Methods
	//---------------------------------------------
	// Following methods are TransitionHandler implementations. Each change item has own transition handlers for 
	// process affected item depending on affected item properties.
	//---------------------------------------------
	
    //+++ Added for IR-016676
	private static void InReview_InWork(IList<AffectedItem> affectedItems)
	{
		IList<ChangeControlledItem> newItemIdItemList =
			(from affectedItem in affectedItems
				where
				affectedItem.NewItemIdItem != null &&
				affectedItem.NewItemIdItem.State != "Released" &&
				affectedItem.NewItemIdItem.State != "Preliminary"
				select affectedItem.NewItemIdItem).ToList();

		newItemIdItemList.Promote("Preliminary");
	}
	//--- Added for IR-016676

	private static void InWork_InReview(IList<AffectedItem> affectedItems)
	{
		IList<ChangeControlledItem> newItemIdItemList =
			(from affectedItem in affectedItems
				where
				affectedItem.NewItemIdItem != null &&
				affectedItem.NewItemIdItem.State != "Released" &&
				affectedItem.NewItemIdItem.State != "In Review"
				select affectedItem.NewItemIdItem).ToList();

		newItemIdItemList.Promote("In Review");
	}

	private static void InReview_Released(IList<AffectedItem> affectedItems)
	{
		IList<ChangeControlledItem> newItemIdItemList =
			(from affectedItem in affectedItems
				where
				affectedItem.NewItemIdItem != null &&
				affectedItem.NewItemIdItem.State != "Released"
				select affectedItem.NewItemIdItem).ToList();

		newItemIdItemList.Promote("Released");

		IList<ChangeControlledItem> affectedIdItemList =
			(from affectedItem in affectedItems
				where
				affectedItem.AffectedIdItem != null &&
				affectedItem.AffectedIdItem.State == "In Change"
				select affectedItem.AffectedIdItem).ToList();

		affectedIdItemList.Promote("Superseded");
	}
	#endregion
}

/// <summary>
/// Represent inheritor of ChangeItem
/// </summary>
class SimpleECO : ChangeItem
{
	public SimpleECO(Item item)
		: base(item, "Simple ECO Affected Item")
	{
	}

	/// <summary>
	/// Validate state item(not on promote)
	/// </summary>
	/// <returns></returns>
	/// <exception cref="ItemErrorException">throw if error</exception>
	protected override ResultStatus ValidateState()
	{
		ResultStatus status = base.ValidateState();
		if (!status.IsSuccess) return status;
		IChangeItemValidationRules thisValidationRules = (IChangeItemValidationRules)this;

		switch (this.State)
		{
			case "New":
				thisValidationRules.IsAffectedItemExists(status);
				thisValidationRules.IsAffectedItemOtherThanNoneActionExists(status);

				foreach (IAffectedItemValidationRules affectedItem in this.AffectedItems)
				{
					affectedItem.Compare_AffectedId_and_NewItem_IdTypes(status);
					affectedItem.Is_AffectedId_and_NewItemId_NotLocked(status);
					affectedItem.Is_AffectedId_InReleasedState(status);
					affectedItem.Is_NewItemId_InPreliminaryState(status);
					affectedItem.Is_AffectedId_Requiered(status);
					affectedItem.Is_NewItemId_Required(status);
					affectedItem.Is_NewItemId_RequiresNull(status);
					affectedItem.Is_AffectedId_RequiresNull(status);
					affectedItem.Is_NewItemId_HasNoPreviouslyReleasedGeneration(status);
				}
				break;
			case "In Work":
			case "In Review":
				thisValidationRules.IsAffectedItemExists(status);
				thisValidationRules.IsAffectedItemOtherThanNoneActionExists(status);

				foreach (IAffectedItemValidationRules affectedItem in this.AffectedItems)
				{
					affectedItem.Compare_AffectedId_and_NewItem_IdTypes(status);
				}
				break;
		}

		return status;
	}

	/// <summary>
	/// Validate item on promote
	/// </summary>
	/// <returns></returns>
	/// <exception cref="ItemErrorException">throw if error</exception>
	protected override ResultStatus ValidateTransition()
	{
		ResultStatus status = base.ValidateTransition();
		if (!status.IsSuccess) return status;

		IChangeItemValidationRules thisValidationRules = (IChangeItemValidationRules)this;

		switch (this.Transition.ToString())
		{
			case "New->In Work":
				thisValidationRules.IsAffectedItemExists(status);
				thisValidationRules.IsAffectedItemOtherThanNoneActionExists(status);

				foreach (IAffectedItemValidationRules affectedItem in this.AffectedItems)
				{
					affectedItem.Compare_AffectedId_and_NewItem_IdTypes(status);
					affectedItem.Is_AffectedId_and_NewItemId_NotLocked(status);
					affectedItem.Is_AffectedId_InReleasedState(status);
					affectedItem.Is_NewItemId_InPreliminaryState(status);
					affectedItem.Is_AffectedId_Requiered(status);
					affectedItem.Is_NewItemId_Required(status);
					affectedItem.Is_NewItemId_RequiresNull(status);
					affectedItem.Is_AffectedId_RequiresNull(status);
					affectedItem.Is_NewItemId_HasNoPreviouslyReleasedGeneration(status);
				}
				break;
			case "In Work->In Review":
			case "In Review->Released":
				thisValidationRules.IsAffectedItemExists(status);
				thisValidationRules.IsAffectedItemOtherThanNoneActionExists(status);

				foreach (IAffectedItemValidationRules affectedItem in this.AffectedItems)
				{
					affectedItem.Compare_AffectedId_and_NewItem_IdTypes(status);
				}
				break;
		}

		return status;
	}

	/// <summary>
	/// Process each affected item by TransitionHandler
	/// </summary>
	/// <param name="handler">Handler which will be executed for each affected item in collection</param>
	/// <returns></returns>
	public override Item ProcessTransition()
	{
		TransitionHandler handler = null;

		switch (this.Transition.ToString())
		{
			case "New->In Work":
				IList<AffectedItem> sortedAffectedItemList = this.AffectedItems;

				#region Promote all not null affectedIdItem to In Change
				{
					IList<ChangeControlledItem> affectedIdItemList =
						(from affectedItem in sortedAffectedItemList
							where affectedItem.AffectedIdItem != null
							select affectedItem.AffectedIdItem)
						.ToList();

					affectedIdItemList.Promote("In Change");
				}
				#endregion

				#region Version affectedIdItem with action Change and Interchangeable = true
				{
					IList<AffectedItem> affectedItemList =
					(from affectedItem in sortedAffectedItemList
						where
						affectedItem.Action == AffectedItem.ActionType.Change &&
						affectedItem.Interchangeable
						select affectedItem)
					.ToList();

					IList<ChangeControlledItem> versionedAffectedIdItemList =
						affectedItemList
						.Select(affectedItem => affectedItem.AffectedIdItem)
						.ToList()
						.Version();

					versionedAffectedIdItemList.Unlock();

					affectedItemList = affectedItemList.Select((AffectedItem affectedItem, int index) =>
					{
						affectedItem.NewItemIdItem = versionedAffectedIdItemList[index];
						return affectedItem;
					}
					)
					.ToList();

					affectedItemList.Lock();
					affectedItemList.ApplyUpdate("new_item_id");
					affectedItemList.Unlock();
				}
				#endregion
				break;
			case "In Work->In Review":
				handler = InWork_InReview;
				break;
			//+++ Added for IR-018084
			case "In Review->In Work":
				handler = InReview_InWork;
				break;
			//--- Added for IR-018084
			case "In Review->Released":
				handler = InReview_Released;
				break;
		}

		return ProcessTransitionHandler(handler);
	}

	#region Dispatch Methods
	//---------------------------------------------
	// Following methods are TransitionHandler implementations. Each change item has own transition handlers for 
	// process affected item depending on affected item properties.
	//---------------------------------------------

    //+++ Added for IR-018084
	private static void InReview_InWork(IList<AffectedItem> affectedItems)
	{
		IList<ChangeControlledItem> newItemIdItemList =
			(from affectedItem in affectedItems
				where
				affectedItem.NewItemIdItem != null &&
				affectedItem.NewItemIdItem.State != "Released" &&
				affectedItem.NewItemIdItem.State != "Preliminary"
				select affectedItem.NewItemIdItem).ToList();

		newItemIdItemList.Promote("Preliminary");
	}
	//--- Added for IR-018084

	private static void InWork_InReview(IList<AffectedItem> affectedItems)
	{
		IList<ChangeControlledItem> newItemIdList =
			(from affectedItem in affectedItems
				where
				affectedItem.NewItemIdItem != null &&
				affectedItem.NewItemIdItem.State != "Released" &&
				affectedItem.NewItemIdItem.State != "In Review"
				select affectedItem.NewItemIdItem).ToList();

		newItemIdList.Promote("In Review");
	}

	private static void InReview_Released(IList<AffectedItem> affectedItems)
	{
		IList<ChangeControlledItem> newItemIdItemList =
			(from affectedItem in affectedItems
				where
				affectedItem.NewItemIdItem != null &&
				affectedItem.NewItemIdItem.State != "Released"
				select affectedItem.NewItemIdItem).ToList();

		newItemIdItemList.Promote("Released");

		IList<ChangeControlledItem> affectedIdItemList =
			(from affectedItem in affectedItems
				where
				affectedItem.AffectedIdItem != null &&
				affectedItem.AffectedIdItem.State == "In Change"
				select affectedItem.AffectedIdItem).ToList();

		affectedIdItemList.Promote("Superseded");
	}

	#endregion
}

/// <summary>
/// Represent inheritor of ChangeItem
/// </summary>
class ExpressECO : ChangeItem
{
	public ExpressECO(Item item)
		: base(item, "Express ECO Affected Item")
	{
	}

	#region Relationships
	RelationshipItemList<AffectedItem> p_aff_list;
	/// <summary>
	/// List of ExtendedAffected Items
	/// </summary>
	public override RelationshipItemList<AffectedItem> AffectedItems
	{
		get
		{
			if (p_aff_list == null)
			{
				p_aff_list = new RelationshipItemList<AffectedItem>(this.AffectedItemRelshipName, new ExtendedAffectedItem(), this);
			}
			return p_aff_list;
		}
	}
	#endregion

	/// <summary>
	/// Validate state item(not on promote)
	/// </summary>
	/// <returns></returns>
	/// <exception cref="ItemErrorException">throw if error</exception>
	protected override ResultStatus ValidateState()
	{
		ResultStatus status = base.ValidateState();
		if (!status.IsSuccess) return status;
		IChangeItemValidationRules thisValidationRules = (IChangeItemValidationRules)this;

		switch (this.State)
		{
			case "In Planning":
			case "Plan Review":
				thisValidationRules.IsAffectedItemExists(status);
				thisValidationRules.IsAffectedItemOtherThanNoneActionExists(status);

				foreach (ExtendedAffectedItem affectedItem in this.AffectedItems)
				{
					IExtendedAffectedItemValidationRules affItemRules = (IExtendedAffectedItemValidationRules)affectedItem;
					affItemRules.Compare_AffectedId_and_NewItem_IdTypes(status);
					affItemRules.Is_AffectedId_and_NewItemId_NotLocked(status);
					affItemRules.Is_AffectedId_InReleasedState(status);
					affItemRules.Is_NewItemId_InPreliminaryState(status);
					affItemRules.Is_AffectedId_Requiered(status);
					affItemRules.Is_NewItemId_Required(status);
					affItemRules.Is_NewItemId_RequiresNull(status);
					affItemRules.Is_AffectedId_RequiresNull(status);
					affItemRules.Is_NewItemId_HasNoPreviouslyReleasedGeneration(status);

					affItemRules.Is_AffectedItem_Action_Review(status);
					affItemRules.Is_NewItemId_InReleasedState(status);
					affItemRules.Is_AffectedId_InSupersededOrReleasedState(status);
					affItemRules.Is_AffectedRelationship_Allowed(status);

					foreach (AffectedRelationship affectedRelationship in affectedItem.AffectedRelationships)
					{
						IAffectedRelationshipValidationRules affRelshRules = (IAffectedRelationshipValidationRules)affectedRelationship;
						affRelshRules.Is_AffectedRelationship_RequiresAttachAction(status);
						affRelshRules.Is_AffectedRelId_Required(status);
						affRelshRules.Is_AffectedRelId_Required_AffectedId_SourceID(status);
						affRelshRules.Is_AffectedRelationshipProperty_Required(status);
					}
				}
				break;
			case "In Work":
			case "In Review":
				thisValidationRules.IsAffectedItemExists(status);
				thisValidationRules.IsAffectedItemOtherThanNoneActionExists(status);

				foreach (ExtendedAffectedItem affectedItem in this.AffectedItems)
				{
					IExtendedAffectedItemValidationRules affItemRules = (IExtendedAffectedItemValidationRules)affectedItem;
					affItemRules.Compare_AffectedId_and_NewItem_IdTypes(status);
					affItemRules.VerifyAffectedItemProperties(status);

					foreach (AffectedRelationship affectedRelationship in affectedItem.AffectedRelationships)
					{
						IAffectedRelationshipValidationRules affRelshRules = (IAffectedRelationshipValidationRules)affectedRelationship;
						affRelshRules.Verify(status);
					}
				}
				break;
		}

		return status;
	}

	/// <summary>
	/// Validate item on promote
	/// </summary>
	/// <returns></returns>
	/// <exception cref="ItemErrorException">throw if error</exception>
	protected override ResultStatus ValidateTransition()
	{
		ResultStatus status = base.ValidateTransition();
		if (!status.IsSuccess) return status;
		IChangeItemValidationRules thisValidationRules = (IChangeItemValidationRules)this;

		switch (this.Transition.ToString())
		{
			case "In Planning->In Work":
			case "Plan Review->In Work":
				thisValidationRules.IsAffectedItemExists(status);
				thisValidationRules.IsAffectedItemOtherThanNoneActionExists(status);

				foreach (ExtendedAffectedItem affectedItem in this.AffectedItems)
				{
					IExtendedAffectedItemValidationRules affItemRules = (IExtendedAffectedItemValidationRules)affectedItem;
					affItemRules.Compare_AffectedId_and_NewItem_IdTypes(status);
					affItemRules.Is_AffectedId_and_NewItemId_NotLocked(status);
					affItemRules.Is_AffectedId_InReleasedState(status);
					affItemRules.Is_NewItemId_InPreliminaryState(status);
					affItemRules.Is_AffectedId_Requiered(status);
					affItemRules.Is_NewItemId_Required(status);
					affItemRules.Is_NewItemId_RequiresNull(status);
					affItemRules.Is_AffectedId_RequiresNull(status);
					affItemRules.Is_NewItemId_HasNoPreviouslyReleasedGeneration(status);

					affItemRules.Is_AffectedItem_Action_Review(status);
					affItemRules.Is_NewItemId_InReleasedState(status);
					affItemRules.Is_AffectedId_InSupersededOrReleasedState(status);
					affItemRules.Is_AffectedRelationship_Allowed(status);

					foreach (AffectedRelationship affectedRelationship in affectedItem.AffectedRelationships)
					{
						IAffectedRelationshipValidationRules affRelshRules = (IAffectedRelationshipValidationRules)affectedRelationship;
						affRelshRules.Is_AffectedRelationship_RequiresAttachAction(status);
						affRelshRules.Is_AffectedRelId_Required(status);
						affRelshRules.Is_AffectedRelId_Required_AffectedId_SourceID(status);
						affRelshRules.Is_AffectedRelationshipProperty_Required(status);
					}
				}
				break;
			case "In Work->In Review":
				thisValidationRules.IsAffectedItemExists(status);
				thisValidationRules.IsAffectedItemOtherThanNoneActionExists(status);

				foreach (IAffectedItemValidationRules affectedItem in this.AffectedItems)
				{
					affectedItem.Compare_AffectedId_and_NewItem_IdTypes(status);
				}
				break;
			case "In Work->Released":
			case "In Review->Released":
				thisValidationRules.IsAffectedItemExists(status);
				thisValidationRules.IsAffectedItemOtherThanNoneActionExists(status);

				foreach (ExtendedAffectedItem affectedItem in this.AffectedItems)
				{
					IExtendedAffectedItemValidationRules affItemRules = (IExtendedAffectedItemValidationRules)affectedItem;
					affItemRules.Compare_AffectedId_and_NewItem_IdTypes(status);
					affItemRules.VerifyAffectedItemProperties(status);

					foreach (AffectedRelationship affectedRelationship in affectedItem.AffectedRelationships)
					{
						IAffectedRelationshipValidationRules affRelshRules = (IAffectedRelationshipValidationRules)affectedRelationship;
						affRelshRules.Verify(status);
					}
				}


				break;
		}

		return status;
	}

	#region Validation Rules
	protected override void IsAffectedItemOtherThanNoneActionExists(ResultStatus status)
	{
		if (this.AffectedItems.Count > 0 && !this.AffectedItems.Any(affectedItem => ((ExtendedAffectedItem)affectedItem).ItemAction != ExtendedAffectedItem.ItemActionType.None))
		{
			status.AddError("ValidateChangeItem_IsAffectedItemWithActionOtherThanNoneExists", this.Type, this.Number);
		}
	}
	#endregion

	/// <summary>
	/// Process each affected item by TransitionHandler
	/// </summary>
	/// <param name="handler">Handler which will be executed for each affected item in collection</param>
	/// <returns></returns>
	public override Item ProcessTransition()
	{
		TransitionHandler handler = null;

		switch (this.Transition.ToString())
		{
			case "In Planning->In Work":
			case "Plan Review->In Work":
				handler = To_InWork;
				break;
			case "In Review->In Work":
				handler = InReview_InWork;
				break;
			case "In Work->In Review":
				handler = InWork_InReview;
				break;
			case "In Work->Released":
			case "In Review->Released":
				handler = Changes_Released;
				break;
		}

		return ProcessTransitionHandler(handler);
	}

	#region Dispatch Methods
	//---------------------------------------------
	// Following methods are TransitionHandler implementations. Each change item has own transition handlers for 
	// process affected item depending on affected item properties.
	//---------------------------------------------

	private static void To_InWork(IList<AffectedItem> affectedItems)
	{
		#region Release
		{
			//Select ItemAction == Release
			IList<ExtendedAffectedItem> releaseExtAffectedItemList =
				affectedItems
				.Where(
					(AffectedItem affectedItem) =>
					{
						ExtendedAffectedItem extAffectedItem = (ExtendedAffectedItem)affectedItem;
						return (extAffectedItem.ItemAction == ExtendedAffectedItem.ItemActionType.Release);
					}
				)
				.Select(affectedItem => (ExtendedAffectedItem)affectedItem)
				.ToList();
			releaseExtAffectedItemList.ProcessAffectedRelationships();
			releaseExtAffectedItemList.ProcessAffectedItemProperties();
		}
		#endregion

		#region Revise
		{
			//Select ItemAction == Revise
			IList<ExtendedAffectedItem> reviseExtAffectedItemList =
				affectedItems
				.Where(
					(AffectedItem affectedItem) =>
					{
						ExtendedAffectedItem extAffectedItem = (ExtendedAffectedItem)affectedItem;
						return (extAffectedItem.ItemAction == ExtendedAffectedItem.ItemActionType.Revise);
					}
				)
				.Select(affectedItem => (ExtendedAffectedItem)affectedItem)
				.ToList();

			//Select AffectedIdItem
			IList<ChangeControlledItem> affectedIdItemList = reviseExtAffectedItemList
				.Select(affectedItem => affectedItem.AffectedIdItem)
				.ToList();
			IList<ChangeControlledItem> versionedItemList = affectedIdItemList.Version();

			versionedItemList.Unlock();

			//Set NewItemIdItem by ids of versioned AffectedIdItems
			reviseExtAffectedItemList = reviseExtAffectedItemList
				.Select(
					(ExtendedAffectedItem affectedItem, int index) =>
					{
						affectedItem.NewItemIdItem = versionedItemList.ElementAt(index);
						return affectedItem;
					}
				)
				.ToList();

			//Apply chanes
			reviseExtAffectedItemList.Lock();
			reviseExtAffectedItemList.ApplyUpdate("new_item_id");
			reviseExtAffectedItemList.Unlock();

			affectedIdItemList.Promote("In Change");

			reviseExtAffectedItemList.ProcessAffectedRelationships();
			reviseExtAffectedItemList.ProcessAffectedItemProperties();
		}
		#endregion

		#region Renumber
		{
			//Select ItemAction == Renumber
			IList<ExtendedAffectedItem> renumberExtAffectedItemList =
				affectedItems
				.Where(
					(AffectedItem affectedItem) =>
					{
						ExtendedAffectedItem extAffectedItem = (ExtendedAffectedItem)affectedItem;
						return (extAffectedItem.ItemAction == ExtendedAffectedItem.ItemActionType.Renumber);
					}
				)
				.Select(affectedItem => (ExtendedAffectedItem)affectedItem)
				.ToList();

			//Select AffectedIdItem
			IList<ChangeControlledItem> affectedIdItemList = renumberExtAffectedItemList
				.Select(affectedItem => affectedItem.AffectedIdItem)
				.ToList();
			IList<ChangeControlledItem> copiedItemList = affectedIdItemList.Copy();

			copiedItemList.Unlock();

			//Set NewItemIdItem by ids of copied AffectedIdItems
			renumberExtAffectedItemList = renumberExtAffectedItemList
				.Select(
					(ExtendedAffectedItem affectedItem, int index) =>
					{
						affectedItem.NewItemIdItem = copiedItemList.ElementAt(index);
						return affectedItem;
					}
				)
				.ToList();

			//Apply changes
			renumberExtAffectedItemList.Lock();
			renumberExtAffectedItemList.ApplyUpdate("new_item_id");
			renumberExtAffectedItemList.Unlock();

			affectedIdItemList.Promote("In Change");

			renumberExtAffectedItemList.ProcessAffectedRelationships();
			renumberExtAffectedItemList.ProcessAffectedItemProperties();
		}
		#endregion
	}

	private static void InWork_InReview(IList<AffectedItem> affectedItems)
	{
		//Select NewItemIdItem where AffectedItem.ItemAction == Release | Revise | Renumber
		IList<ChangeControlledItem> newItemList =
			affectedItems
			.Where(
				(AffectedItem affectedItem) =>
				{
					ExtendedAffectedItem extAffectedItem = (ExtendedAffectedItem)affectedItem;
					return (extAffectedItem.ItemAction == ExtendedAffectedItem.ItemActionType.Release ||
						extAffectedItem.ItemAction == ExtendedAffectedItem.ItemActionType.Revise ||
						extAffectedItem.ItemAction == ExtendedAffectedItem.ItemActionType.Renumber);
				}
			)
			.Select(affectedItem => affectedItem.NewItemIdItem)
			.ToList();

		newItemList.Promote("In Review");
	}

	private static void InReview_InWork(IList<AffectedItem> affectedItems)
	{
		//Select NewItemIdItem where AffectedItem.ItemAction == Release | Revise | Renumber
		IList<ChangeControlledItem> newItemList =
			affectedItems
			.Where(
				(AffectedItem affectedItem) =>
				{
					ExtendedAffectedItem extAffectedItem = (ExtendedAffectedItem)affectedItem;
					return (extAffectedItem.ItemAction == ExtendedAffectedItem.ItemActionType.Release ||
						extAffectedItem.ItemAction == ExtendedAffectedItem.ItemActionType.Revise ||
						extAffectedItem.ItemAction == ExtendedAffectedItem.ItemActionType.Renumber);
				}
			)
			.Select(affectedItem => affectedItem.NewItemIdItem)
			.ToList();

		newItemList.Promote("Preliminary");
	}

	private static void Changes_Released(IList<AffectedItem> affectedItems)
	{
		//Select NewItemIdItem where AffectedItem.ItemAction == Release | Revise | Renumber
		IList<ChangeControlledItem> newItemList =
			affectedItems
			.Where(
				(AffectedItem affectedItem) =>
				{
					ExtendedAffectedItem extAffectedItem = (ExtendedAffectedItem)affectedItem;
					return (extAffectedItem.ItemAction == ExtendedAffectedItem.ItemActionType.Release ||
						extAffectedItem.ItemAction == ExtendedAffectedItem.ItemActionType.Revise ||
						extAffectedItem.ItemAction == ExtendedAffectedItem.ItemActionType.Renumber);
				}
			)
			.Select(affectedItem => affectedItem.NewItemIdItem)
			.ToList();
		newItemList.Promote("Released");

		//Select AffectedIdItem where AffectedItem.ItemAction == Supersede | Revise | Renumber
		IList<ChangeControlledItem> supersedeItemList =
			affectedItems
			.Where(
				(AffectedItem affectedItem) =>
				{
					ExtendedAffectedItem extAffectedItem = (ExtendedAffectedItem)affectedItem;
					return (extAffectedItem.ItemAction == ExtendedAffectedItem.ItemActionType.Revise ||
						extAffectedItem.ItemAction == ExtendedAffectedItem.ItemActionType.Renumber ||
						extAffectedItem.ItemAction == ExtendedAffectedItem.ItemActionType.Supersede);
				}
			)
			.Select(affectedItem => affectedItem.AffectedIdItem)
			.ToList();
		supersedeItemList.Promote("Superseded");

		//Select AffectedIdItem where AffectedItem.ItemAction == Obsolete
		IList<ChangeControlledItem> obsoleteItemList =
			affectedItems
			.Where(
				(AffectedItem affectedItem) =>
				{
					ExtendedAffectedItem extAffectedItem = (ExtendedAffectedItem)affectedItem;
					return (extAffectedItem.ItemAction == ExtendedAffectedItem.ItemActionType.Obsolete);
				}
			)
			.Select(affectedItem => affectedItem.AffectedIdItem)
			.ToList();
		obsoleteItemList.Promote("Obsolete");
	}
	#endregion
}

/// <summary>
/// Represent inheritor of ChangeItem
/// </summary>
class ExpressDCO : ChangeItem
{
	public ExpressDCO(Item item)
		: base(item, "Express DCO Affected Item")
	{
	}


	/// <summary>
	/// Validate state item(not on promote)
	/// </summary>
	/// <returns></returns>
	protected override ResultStatus ValidateState()
	{
		ResultStatus status = base.ValidateState();
		if (!status.IsSuccess) return status;
		IChangeItemValidationRules thisValidationRules = (IChangeItemValidationRules)this;

		switch (this.State)
		{
			case "New":
				thisValidationRules.IsAffectedItemExists(status);
				thisValidationRules.IsAffectedItemOtherThanNoneActionExists(status);

				foreach (IAffectedItemValidationRules affectedItem in this.AffectedItems)
				{
					affectedItem.Compare_AffectedId_and_NewItem_IdTypes(status);
					affectedItem.Is_AffectedId_and_NewItemId_NotLocked(status);
					affectedItem.Is_AffectedId_InReleasedState(status);
					affectedItem.Is_NewItemId_InPreliminaryState(status);
					affectedItem.Is_AffectedId_Requiered(status);
					affectedItem.Is_NewItemId_Required(status);
					affectedItem.Is_NewItemId_RequiresNull(status);
					affectedItem.Is_AffectedId_RequiresNull(status);
					affectedItem.Is_NewItemId_HasNoPreviouslyReleasedGeneration(status);
				}
				break;
			case "In Work":
			case "In Review":
				thisValidationRules.IsAffectedItemExists(status);
				thisValidationRules.IsAffectedItemOtherThanNoneActionExists(status);

				foreach (IAffectedItemValidationRules affectedItem in this.AffectedItems)
				{
					affectedItem.Compare_AffectedId_and_NewItem_IdTypes(status);
				}
				break;
		}
		return status;
	}

	/// <summary>
	/// Validate item on promote
	/// </summary>
	/// <returns></returns>
	/// <exception cref="ItemErrorException">throw if error</exception>
	protected override ResultStatus ValidateTransition()
	{
		ResultStatus status = base.ValidateTransition();
		if (!status.IsSuccess) return status;
		IChangeItemValidationRules thisValidationRules = (IChangeItemValidationRules)this;

		switch (this.Transition.ToString())
		{
			case "New->In Work":
				thisValidationRules.IsAffectedItemExists(status);
				thisValidationRules.IsAffectedItemOtherThanNoneActionExists(status);

				foreach (IAffectedItemValidationRules affectedItem in this.AffectedItems)
				{
					affectedItem.Compare_AffectedId_and_NewItem_IdTypes(status);
					affectedItem.Is_AffectedId_and_NewItemId_NotLocked(status);
					affectedItem.Is_AffectedId_InReleasedState(status);
					affectedItem.Is_NewItemId_InPreliminaryState(status);
					affectedItem.Is_AffectedId_Requiered(status);
					affectedItem.Is_NewItemId_Required(status);
					affectedItem.Is_NewItemId_RequiresNull(status);
					affectedItem.Is_AffectedId_RequiresNull(status);
					affectedItem.Is_NewItemId_HasNoPreviouslyReleasedGeneration(status);
				}
				break;
			case "In Work->In Review":
			case "In Work->Released":
			case "In Review->Released":
				thisValidationRules.IsAffectedItemExists(status);
				thisValidationRules.IsAffectedItemOtherThanNoneActionExists(status);

				foreach (IAffectedItemValidationRules affectedItem in this.AffectedItems)
				{
					affectedItem.Compare_AffectedId_and_NewItem_IdTypes(status);
				}
				break;
		}

		return status;
	}

	/// <summary>
	/// Process each affected item by TransitionHandler
	/// </summary>
	/// <param name="handler">Handler which will be executed for each affected item in collection</param>
	/// <returns></returns>
	public override Item ProcessTransition()
	{
		TransitionHandler handler = null;
		switch (this.Transition.ToString())
		{
			case "New->In Work":
				handler = New_InWork;
				break;
			case "In Work->In Review":
				handler = InWork_InReview;
				break;
			case "In Review->In Work":
				handler = InReview_InWork;
				break;
			case "In Work->Released":
			case "In Review->Released":
				handler = Changes_Released;
				break;
		}

		return ProcessTransitionHandler(handler);
	}

	#region Dispatch Methods
	//---------------------------------------------
	// Following methods are TransitionHandler implementations. Each change item has own transition handlers for 
	// process affected item depending on affected item properties.
	//---------------------------------------------


	private static void New_InWork(IList<AffectedItem> affectedItems)
	{
		#region Change
		{
			//Select Action == Change && Interchangeable == true  
			IList<AffectedItem> changeInterchangeableAffectedItemList =
				(from affectedItem in affectedItems
					where
					affectedItem.Action == AffectedItem.ActionType.Change
					&&
					affectedItem.Interchangeable == true
					select affectedItem)
				.ToList();

			//Version AffectedIdItem
			IList<ChangeControlledItem> versionedAffectedIdItemList =
				changeInterchangeableAffectedItemList
				.Select(affectedItem => affectedItem.AffectedIdItem)
				.ToList()
				.Version();

			versionedAffectedIdItemList.Unlock();

			//Set NewItemIdItem by new id of versioned AffectedIdItem
			changeInterchangeableAffectedItemList = changeInterchangeableAffectedItemList
				.Select(
					(AffectedItem affectedItem, int index) =>
					{
						affectedItem.NewItemIdItem = versionedAffectedIdItemList[index];
						return affectedItem;
					}
				)
				.ToList();

			//Apply changes
			changeInterchangeableAffectedItemList.Lock();
			changeInterchangeableAffectedItemList.ApplyUpdate("new_item_id");
			changeInterchangeableAffectedItemList.Unlock();

			//Select Action == Change
			IList<ChangeControlledItem> changeAffectedIdItemList =
			(from affectedItem in affectedItems
				where
				affectedItem.Action == AffectedItem.ActionType.Change
				select affectedItem.AffectedIdItem)
			.ToList();

			//Promote AffectedIdItem
			changeAffectedIdItemList.Promote("In Change");
		}
		#endregion

		#region Delete
		{
			//Select Delete == Change
			IEnumerable<ChangeControlledItem> deleteAffectedIdItemList =
			(from affectedItem in affectedItems
				where
				affectedItem.Action == AffectedItem.ActionType.Delete
				select affectedItem.AffectedIdItem);

			//Select AffectedIdItem and promote them
			deleteAffectedIdItemList
				.ToList()
				.Promote("In Change");
		}
		#endregion
	}

	private static void InWork_InReview(IList<AffectedItem> affectedItems)
	{
		//Select NewItemIdItem where affectedItem Action == Add | Change
		IList<ChangeControlledItem> newItemList =
			(from affectedItem in affectedItems
				where
				affectedItem.Action == AffectedItem.ActionType.Add ||
				affectedItem.Action == AffectedItem.ActionType.Change
				select affectedItem.NewItemIdItem)
			.ToList();

		newItemList.Promote("In Review");
	}

	private static void InReview_InWork(IList<AffectedItem> affectedItems)
	{
		//Select NewItemIdItem where affectedItem Action == Add | Change
		IList<ChangeControlledItem> newItemList =
		(from affectedItem in affectedItems
			where
			affectedItem.Action == AffectedItem.ActionType.Add ||
			affectedItem.Action == AffectedItem.ActionType.Change
			select affectedItem.NewItemIdItem)
		.ToList();

		newItemList.Promote("Preliminary");
	}

	private static void Changes_Released(IList<AffectedItem> affectedItems)
	{
		//Select NewItemIdItem where affectedItem Action == Add | Change
		IList<ChangeControlledItem> newItemList =
		(from affectedItem in affectedItems
			where
			affectedItem.Action == AffectedItem.ActionType.Add ||
			affectedItem.Action == AffectedItem.ActionType.Change
			select affectedItem.NewItemIdItem)
		.ToList();

		newItemList.Promote("Released");

		//Select AffectedIdItem where affectedItem Action == Delete | Change
		IList<ChangeControlledItem> affectedItemList =
		(from affectedItem in affectedItems
			where
			affectedItem.Action == AffectedItem.ActionType.Delete ||
			affectedItem.Action == AffectedItem.ActionType.Change
			select affectedItem.AffectedIdItem)
		.ToList();

		affectedItemList.Promote("Superseded");
	}
	#endregion
}


class ChangeControlledRelationship : Relationship
{
	public ChangeControlledRelationship()
		: base()
	{
	}

	public ChangeControlledRelationship(Item item)
		: base(item, null)
	{
		// replace Change Controlled Relationship poly type to particular type
		// TODO: use itemtype property to detect defned type if Change Controlled Relationship will contain other than Part BOM relationships
		item.setAttribute("type", "Part BOM");
	}

	public ChangeControlledRelationship(Item item, ItemContext sourceItem)
		: base(item, sourceItem)
	{
		// replace Change Controlled Relationship poly type to particular type
		// TODO: use itemtype property to detect defned type if Change Controlled Relationship will contain other than Part BOM relationships
		item.setAttribute("type", "Part BOM");
	}

	#region Mapped Properties
	public string SourceID
	{
		get { return this.Item.getProperty("source_id"); }
		set { this.Item.setProperty("source_id", value); }
	}

	public string SortOrder
	{
		get { return this.Item.getProperty("sort_order"); }
	}

	public string ItemTypeID
	{
		get { return this.Item.getProperty("itemtype"); }
	}
	#endregion

	public override void Delete()
	{
		Item tmpItem = Utils.Innovator.newItem();
		tmpItem.setAttribute("typeId", this.ItemTypeID);
		tmpItem.setID(this.ID);
		tmpItem.setAction("delete");

		Item res = tmpItem.apply();

		Utils.AssertItem(res);

		FireDeleted();
	}
}

/// <summary>
/// Wrap Change Controlled Item type instance
/// </summary>
class ChangeControlledItem : ItemContext
{
	public ChangeControlledItem(Item item)
		: base(item)
	{
		p_relationships = new RelationshipItemList<ChangeControlledRelationship>("Change Controlled Relationship", new ChangeControlledRelationship(), this);
	}

	#region Mapped Properties
	public string ItemNumber
	{
		get { return this.Item.getProperty("item_number", ""); }
	}
	#endregion

	#region Relationships
	public RelationshipItemList<ChangeControlledRelationship> p_relationships;
	public RelationshipItemList<ChangeControlledRelationship> Relationships
	{
		get { return p_relationships; }
	}
	#endregion

	/// <summary>
	/// Is item has previosly released generations
	/// </summary>
	public bool HasPreviouslyReleasedGenerations
	{
		get
		{
			Item thisCheck = Utils.Innovator.newItem(this.Type, "get");
			thisCheck.setAttribute("select", "config_id");
			thisCheck.setProperty("generation", "*");
			thisCheck.setPropertyCondition("generation", "like");

			thisCheck.setProperty("config_id", this.ConfigID);
			thisCheck.setProperty("is_released", "1");
			thisCheck = thisCheck.apply();

			return thisCheck.getItemCount() > 0;
		}
	}
}

/// <summary>
/// Wrap Life Cycle Transition item type instance
/// </summary>
class LifeCycleTransition : ItemContext
{
	public LifeCycleTransition(Item item)
		: base(item)
	{

	}

	public string ToState
	{
		get { return this.Item.getPropertyAttribute("to_state", "keyed_name", ""); }
	}

	public string FromState
	{
		get { return this.Item.getPropertyAttribute("from_state", "keyed_name", ""); }
	}

	public override string ToString()
	{
		return String.Format(CultureInfo.InvariantCulture, "{0}->{1}", this.FromState, this.ToState);
	}
}

/// <summary>
/// Affected Item wrapper.
/// </summary>
class AffectedItem : RelatedRelationshipItem, IAffectedItemValidationRules
{
	public enum ActionType
	{
		None,
		Add,
		Change,
		Delete
	}

	/// <summary>
	/// Prototype constructor
	/// </summary>
	public AffectedItem()
		: base()
	{
	}

	public AffectedItem(Item item, ItemContext sourceItem)
		: base(item, sourceItem)
	{
		this.Interchangeable = this.Item.getProperty("interchangeable", "") == "1";

		string changeAction = this.Item.getProperty("action", "");
		this.Action = Utils.GetEnumByValue<ActionType>(changeAction, "None");
	}

	#region Mapped Properties
	ActionType p_action;
	public ActionType Action
	{
		get { return p_action; }
		private set { p_action = value; }
	}

	bool p_interchangeable;
	public bool Interchangeable
	{
		get { return p_interchangeable; }
		private set { p_interchangeable = value; }
	}

	ChangeControlledItem p_affected_id_item;
	public ChangeControlledItem AffectedIdItem
	{
		get
		{
			if (p_affected_id_item == null)
			{
				Item affectedItem = this.Item.getPropertyItem("affected_id");
				if (affectedItem == null)
				{
					return null;
				}

				p_affected_id_item = new ChangeControlledItem(affectedItem);
			}
			return p_affected_id_item;
		}

		set
		{
			p_affected_id_item = null;
			this.Item.setPropertyItem("affected_id", value.Item);
		}
	}

	ChangeControlledItem p_new_item_id_item;
	public ChangeControlledItem NewItemIdItem
	{
		get
		{
			if (p_new_item_id_item == null)
			{
				Item newItem = this.Item.getPropertyItem("new_item_id");
				if (newItem == null)
				{
					return null;
				}

				p_new_item_id_item = new ChangeControlledItem(newItem);
			}
			return p_new_item_id_item;
		}

		set
		{
			this.p_new_item_id_item = null;
			this.Item.setPropertyItem("new_item_id", value.Item);
		}
	}

	#endregion

	#region IAffectedItemValidationRules Members
	void IAffectedItemValidationRules.Compare_AffectedId_and_NewItem_IdTypes(ResultStatus status)
	{
		if (this.AffectedIdItem == null || this.NewItemIdItem == null) return;

		if (this.AffectedIdItem.Type != this.NewItemIdItem.Type)
		{
			status.AddError("ValidateChangeItem_Compare_AffectedId_and_NewItem_IdTypes",
				this.AffectedIdItem.Type, this.AffectedIdItem.ItemNumber,
				this.NewItemIdItem.Type, this.NewItemIdItem.ItemNumber);
		}
	}

	void IAffectedItemValidationRules.Is_AffectedId_and_NewItemId_NotLocked(ResultStatus status)
	{
		if (this.AffectedIdItem != null && this.AffectedIdItem.IsLocked)
		{
			status.AddError("ValidateChangeItem_Is_AffectedId_and_NewItemId_NotLocked",
				this.AffectedIdItem.Type, this.AffectedIdItem.ItemNumber);
		}

		if (this.NewItemIdItem != null && this.NewItemIdItem.IsLocked)
		{
			status.AddError("ValidateChangeItem_Is_AffectedId_and_NewItemId_NotLocked",
				this.NewItemIdItem.Type, this.NewItemIdItem.ItemNumber);
		}
	}

	void IAffectedItemValidationRules.Is_AffectedId_InReleasedState(ResultStatus status)
	{
		if (this.AffectedIdItem != null && this.AffectedIdItem.State != "Released")
		{
			status.AddError("ValidateChangeItem_Is_AffectedId_InReleasedState",
				this.AffectedIdItem.Type, this.AffectedIdItem.ItemNumber, this.AffectedIdItem.State);
		}
	}

	void IAffectedItemValidationRules.Is_NewItemId_InPreliminaryState(ResultStatus status)
	{
		if (this.NewItemIdItem != null && this.NewItemIdItem.State != "Preliminary")
		{
			status.AddError("ValidateChangeItem_Is_NewItemId_InPreliminaryState",
				this.NewItemIdItem.Type, this.NewItemIdItem.ItemNumber, this.NewItemIdItem.State);
		}
	}

	void IAffectedItemValidationRules.Is_AffectedId_Requiered(ResultStatus status)
	{
		if (this.Action == AffectedItem.ActionType.Change || this.Action == AffectedItem.ActionType.Delete)
		{
			if (this.AffectedIdItem == null)
			{
				status.AddError("ValidateChangeItem_Is_AffectedId_Requiered");
			}
		}
	}

	void IAffectedItemValidationRules.Is_NewItemId_Required(ResultStatus status)
	{
		if (this.Action == AffectedItem.ActionType.Change && !this.Interchangeable)
		{
			if (this.NewItemIdItem == null)
			{
				if (this.AffectedIdItem == null)
				{
					status.AddError("ValidateChangeItem_Is_NewItemId_Required");
				}
				else
				{
					status.AddError("ValidateChangeItem_Is_NewItemId_Required_with_param", this.AffectedIdItem.Type, this.AffectedIdItem.ItemNumber);
				}
			}
		}

		if (this.Action == AffectedItem.ActionType.Add)
		{
			if (this.NewItemIdItem == null)
			{
				status.AddError("ValidateChangeItem_Is_NewItemId_Required_add_action");
			}
		}
	}

	void IAffectedItemValidationRules.Is_NewItemId_RequiresNull(ResultStatus status)
	{
		if (this.Action == AffectedItem.ActionType.Delete)
		{
			if (this.NewItemIdItem != null)
			{
				if (this.AffectedIdItem == null)
				{
					status.AddError("ValidateChangeItem_Is_NewItemId_RequiresNull_OnDelete");
				}
				else
				{
					status.AddError("ValidateChangeItem_Is_NewItemId_RequiresNull_OnDelete_with_param", this.AffectedIdItem.Type, this.AffectedIdItem.ItemNumber);
				}
			}
		}

		if (this.Action == AffectedItem.ActionType.Change && this.Interchangeable)
		{
			if (this.NewItemIdItem != null)
			{
				if (this.AffectedIdItem == null)
				{
					status.AddError("ValidateChangeItem_Is_NewItemId_RequiresNull_OnChange");
				}
				else
				{
					status.AddError("ValidateChangeItem_Is_NewItemId_RequiresNull_OnChange_with_param", this.AffectedIdItem.Type, this.AffectedIdItem.ItemNumber);
				}
			}
		}
	}

	void IAffectedItemValidationRules.Is_AffectedId_RequiresNull(ResultStatus status)
	{
		if (this.Action == AffectedItem.ActionType.Add)
		{
			if (this.AffectedIdItem != null)
			{
				if (this.NewItemIdItem == null)
				{
					status.AddError("ValidateChangeItem_Is_AffectedId_RequiresNull");
				}
				else
				{
					status.AddError("ValidateChangeItem_Is_AffectedId_RequiresNull_with_param", this.NewItemIdItem.Type, this.NewItemIdItem.ItemNumber);
				}
			}
		}
	}

	void IAffectedItemValidationRules.Is_NewItemId_HasNoPreviouslyReleasedGeneration(ResultStatus status)
	{
		if (this.NewItemIdItem == null) return;

		if (this.Action == AffectedItem.ActionType.Add && this.NewItemIdItem.HasPreviouslyReleasedGenerations)
		{
			status.AddError("ValidateChangeItem_Is_NewItemId_HasNoPreviouslyReleasedGeneration",
				this.NewItemIdItem.Type, this.NewItemIdItem.ItemNumber);
		}
	}
	#endregion
}

/// <summary>
/// Extend AffectedItem class by new added properties and relationships to Affected Item type definition.
/// </summary>
class ExtendedAffectedItem : AffectedItem, IExtendedAffectedItemValidationRules
{
	public enum ItemActionType
	{
		Empty,
		None,
		Release,
		Revise,
		Renumber,
		Supersede,
		Obsolete,
		Review
	}

	public ExtendedAffectedItem()
		: base()
	{
	}

	public ExtendedAffectedItem(Item item, ItemContext sourceItem)
		: base(item, sourceItem)
	{
		string itemChangeAction = this.Item.getProperty("item_action", "");
		this.ItemAction = Utils.GetEnumByValue<ItemActionType>(itemChangeAction, "Empty");

		p_aff_rel = new RelationshipItemList<AffectedRelationship>("Affected Item Relationship", new AffectedRelationship(), this);
		p_aff_item_prop = new RelationshipItemList<AffectedItemProperty>("Affected Item Property", new AffectedItemProperty(), this);
	}

	#region Relationships
	RelationshipItemList<AffectedRelationship> p_aff_rel;
	/// <summary>
	/// Affected Item Relationship relationship
	/// </summary>
	public RelationshipItemList<AffectedRelationship> AffectedRelationships
	{
		get { return p_aff_rel; }
	}

	RelationshipItemList<AffectedItemProperty> p_aff_item_prop;
	/// <summary>
	/// Affected Item Property relationship
	/// </summary>
	public RelationshipItemList<AffectedItemProperty> AffectedItemProperties
	{
		get { return p_aff_item_prop; }
	}
	#endregion

	#region Mapped Properties
	ItemActionType p_item_action;
	public ItemActionType ItemAction
	{
		get { return p_item_action; }
		private set { p_item_action = value; }
	}
	#endregion

	#region IAffectedItemValidationRules Members
	/// <summary>
	/// All affected_id items must be in "Released" state when item_action is "Revise", "Renumber" or "Supersede" or "Obsolete"
	/// </summary>
	/// <param name="status"></param>
	void IAffectedItemValidationRules.Is_AffectedId_InReleasedState(ResultStatus status)
	{
		if (this.ItemAction == ItemActionType.Revise ||
			this.ItemAction == ItemActionType.Renumber ||
			this.ItemAction == ItemActionType.Supersede)
		{
			if (this.AffectedIdItem != null && this.AffectedIdItem.State != "Released")
			{
				status.AddError("ValidateAffectedItem_Is_AffectedId_InReleasedState",
					this.ItemAction.ToString(), this.AffectedIdItem.Type, this.AffectedIdItem.ItemNumber, this.AffectedIdItem.State);
			}
		}
	}

	/// <summary>
	/// All new_item_id items must be in Preliminary state when item_action is "Release"
	/// </summary>
	/// <param name="status"></param>
	void IAffectedItemValidationRules.Is_NewItemId_InPreliminaryState(ResultStatus status)
	{
		if (this.ItemAction == ItemActionType.Release)
		{
			if (this.NewItemIdItem != null && this.NewItemIdItem.State != "Preliminary")
			{
				status.AddError("ValidateAffectedItem_Is_NewItemId_InPreliminaryState",
					this.NewItemIdItem.Type, this.NewItemIdItem.ItemNumber, this.NewItemIdItem.State);
			}
		}
	}

	/// <summary>
	/// affected_id is required when item_action is "Revise", "Renumber", "Supersede" or "Obsolete"
	/// </summary>
	/// <param name="status"></param>
	void IAffectedItemValidationRules.Is_AffectedId_Requiered(ResultStatus status)
	{
		if (this.ItemAction == ItemActionType.Revise ||
			this.ItemAction == ItemActionType.Renumber ||
			this.ItemAction == ItemActionType.Supersede ||
			this.ItemAction == ItemActionType.Obsolete)
		{
			if (this.AffectedIdItem == null)
			{
				status.AddError("ValidateAffectedItem_Is_AffectedId_Requiered", this.ItemAction.ToString());
			}
		}
	}

	/// <summary>
	/// new_item_id is required when action is "Release" or "Supersede"
	/// </summary>
	/// <param name="status"></param>
	void IAffectedItemValidationRules.Is_NewItemId_Required(ResultStatus status)
	{
		if (this.ItemAction == ItemActionType.Release || this.ItemAction == ItemActionType.Supersede)
		{
			if (this.NewItemIdItem == null)
			{
				if (this.AffectedIdItem == null)
				{
					status.AddError("ValidateAffectedItem_Is_NewItemId_Required", this.ItemAction.ToString());
				}
				else
				{
					status.AddError("ValidateAffectedItem_Is_NewItemId_Required_with_param",
						this.ItemAction.ToString(), this.AffectedIdItem.Type, this.AffectedIdItem.ItemNumber);
				}
			}
		}
	}

	/// <summary>
	/// new_item_id must be null when item_action is "Revise", "Renumber" or "Obsolete"
	/// </summary>
	/// <param name="status"></param>
	void IAffectedItemValidationRules.Is_NewItemId_RequiresNull(ResultStatus status)
	{
		if (this.ItemAction == ItemActionType.Revise ||
			this.ItemAction == ItemActionType.Renumber ||
			this.ItemAction == ItemActionType.Obsolete)
		{
			if (this.NewItemIdItem != null)
			{
				if (this.AffectedIdItem == null)
				{
					status.AddError("ValidateAffectedItem_Is_NewItemId_RequiresNull", this.ItemAction.ToString());
				}
				else
				{
					status.AddError("ValidateAffectedItem_Is_NewItemId_RequiresNull_with_param",
						this.ItemAction.ToString(), this.AffectedIdItem.Type, this.AffectedIdItem.ItemNumber);
				}
			}
		}
	}

	/// <summary>
	/// affected_id must be null when item_action is "Release"
	/// </summary>
	/// <param name="status"></param>
	void IAffectedItemValidationRules.Is_AffectedId_RequiresNull(ResultStatus status)
	{
		if (this.ItemAction == ItemActionType.Release)
		{
			if (this.AffectedIdItem != null)
			{
				if (this.NewItemIdItem == null)
				{
					status.AddError("ValidateAffectedItem_Is_AffectedId_RequiresNull");
				}
				else
				{
					status.AddError("ValidateAffectedItem_Is_AffectedId_RequiresNull_with_param",
						this.NewItemIdItem.Type, this.NewItemIdItem.ItemNumber);
				}
			}
		}
	}

	/// <summary>
	/// new_item_id items must not have a previously released generation when action is "Release"
	/// </summary>
	/// <param name="status"></param>
	void IAffectedItemValidationRules.Is_NewItemId_HasNoPreviouslyReleasedGeneration(ResultStatus status)
	{
		if (this.NewItemIdItem == null) return;

		if (this.ItemAction == ItemActionType.Release && this.NewItemIdItem.HasPreviouslyReleasedGenerations)
		{
			status.AddError("ValidateAffectedItem_Is_NewItemId_HasNoPreviouslyReleasedGeneration",
				this.NewItemIdItem.Type, this.NewItemIdItem.ItemNumber);
		}
	}
	#endregion

	#region IExtendedAffectedItemValidationRules Members
	/// <summary>
	/// new_item_id items must not have a previously released generation when action is "Release"
	/// </summary>
	/// <param name="status"></param>
	void IExtendedAffectedItemValidationRules.Is_AffectedItem_Action_Review(ResultStatus status)
	{
		if (this.ItemAction == ItemActionType.Review)
		{
			status.AddError("ValidateAffectedItem_Is_AffectedItem_Action_Review");
		}
	}

	/// <summary>
	/// All affected_id items must be in "Released" or "Superseded" state when item_action is "Obsolete"
	/// </summary>
	/// <param name="status"></param>
	void IExtendedAffectedItemValidationRules.Is_AffectedId_InSupersededOrReleasedState(ResultStatus status)
	{
		if (this.ItemAction == ItemActionType.Obsolete)
		{
			if (this.AffectedIdItem != null && this.AffectedIdItem.State != "Superseded" && this.AffectedIdItem.State != "Released")
			{
				status.AddError("ValidateAffectedItem_Is_AffectedId_InSupersededOrReleasedState",
					this.ItemAction.ToString(), this.AffectedIdItem.Type, this.AffectedIdItem.ItemNumber, this.AffectedIdItem.State);
			}
		}
	}

	/// <summary>
	/// new_item_id items must not have a previously released generation when action is "Release"
	/// </summary>
	/// <param name="status"></param>
	void IExtendedAffectedItemValidationRules.Is_NewItemId_InReleasedState(ResultStatus status)
	{
		if (this.ItemAction == ItemActionType.Supersede)
		{
			if (this.NewItemIdItem != null && this.NewItemIdItem.State != "Released")
			{
				status.AddError("ValidateAffectedItem_Is_NewItemId_InReleasedState",
					this.ItemAction.ToString(), this.NewItemIdItem.Type, this.NewItemIdItem.ItemNumber, this.NewItemIdItem.State);
			}
		}
	}

	/// <summary>
	/// No affected relationships may be attached to affected items with an item_action of "Supersede", "Obsolete" or "None"
	/// </summary>
	/// <param name="status"></param>
	void IExtendedAffectedItemValidationRules.Is_AffectedRelationship_Allowed(ResultStatus status)
	{
		if (this.ItemAction == ItemActionType.Supersede || this.ItemAction == ItemActionType.Obsolete || this.ItemAction == ItemActionType.None)
		{
			if (this.AffectedRelationships.Count > 0)
			{
				status.AddError("ValidateAffectedItem_Is_AffectedRelationship_Allowed", this.ItemAction.ToString());
			}
		}
	}

	/// <summary>
	/// Verify all Affected Item Properties.
	/// </summary>
	void IExtendedAffectedItemValidationRules.VerifyAffectedItemProperties(ResultStatus status)
	{
		foreach (AffectedItemProperty aff_item_prop in this.AffectedItemProperties)
		{
			string value = this.NewItemIdItem.Item.getProperty(aff_item_prop.PropertyName);
			if (value != aff_item_prop.NewValue)
			{
				status.AddError("VerifyAffectedRelationship_NotAllItemPropertiesWereApplied", aff_item_prop.PropertyName, aff_item_prop.NewValue, this.NewItemIdItem.Type, this.NewItemIdItem.ItemNumber);
			}
		}
	}
	#endregion

	#region Members
	/// <summary>
	/// Process all Affected Relationships relationships. In depend of action affected relationship will be Attached/Modified/Removed.
	/// </summary>
	public void ProcessAffectedRelationships()
	{
		List<AffectedRelationship> sortedAffectedRelationships;
		Func<AffectedRelationship.RelationshipAction, int> orderByComparer = (AffectedRelationship.RelationshipAction action) => {
			  int res = 0;
			  switch (action){
			  	case AffectedRelationship.RelationshipAction.Remove:
			  	  res = 1;
			  	  break;
			  	case AffectedRelationship.RelationshipAction.Attach:
			  	  res = 2;
			  	  break;
			  	case AffectedRelationship.RelationshipAction.Modify:
			  	  res = 3;
			  	  break;
			  	case AffectedRelationship.RelationshipAction.Empty:
			  	  res = 4;
			  	  break;
			  	default:
			  	  throw new ArgumentOutOfRangeException("Not supported action in switch, action=" + action);
			  }
			  return res;
			};
		sortedAffectedRelationships = this.AffectedRelationships.OrderBy(x => orderByComparer(x.Action)).ToList();
		foreach (AffectedRelationship aff_relship in sortedAffectedRelationships)
		{
			aff_relship.Process();
		}
	}

	/// <summary>
	/// Process all Affected Item Properties relationships. Apply property to new item.
	/// </summary>
	public void ProcessAffectedItemProperties()
	{
		foreach (AffectedItemProperty aff_item_prop in this.AffectedItemProperties)
		{
			this.NewItemIdItem.Item.setProperty(aff_item_prop.PropertyName, aff_item_prop.NewValue);
		}
		this.NewItemIdItem.ApplyEdit();
	}
	#endregion
}

class AffectedRelationship : RelatedRelationshipItem, IAffectedRelationshipValidationRules
{
	public enum RelationshipAction
	{
		Empty,
		Attach,
		Modify,
		Remove
	}

	public AffectedRelationship()
		: base()
	{
	}

	public AffectedRelationship(Item item, ItemContext sourceItem)
		: base(item, sourceItem)
	{
		string action = this.Item.getProperty("rel_action", "");
		this.Action = Utils.GetEnumByValue<RelationshipAction>(action, "Empty");
		p_aff_rel_prop = new RelationshipItemList<AffectedRelationshipProperty>("Affected Relationship Property", new AffectedRelationshipProperty(), this);
	}

	#region Relationships
	RelationshipItemList<AffectedRelationshipProperty> p_aff_rel_prop;
	public RelationshipItemList<AffectedRelationshipProperty> AffectedRelationshipProperties
	{
		get { return p_aff_rel_prop; }
	}
	#endregion

	#region Mapped Properties
	RelationshipAction p_relAction;
	public RelationshipAction Action
	{
		get { return p_relAction; }
		private set { p_relAction = value; }
	}

	ChangeControlledRelationship p_new_rel_item_id_item;
	public ChangeControlledRelationship NewRelItemIdItem
	{
		get
		{

			if (p_new_rel_item_id_item == null)
			{
				Item newRelItem = this.Item.getPropertyItem("new_rel_id");
				if (newRelItem == null)
				{
					return null;
				}

				p_new_rel_item_id_item = new ChangeControlledRelationship(newRelItem);
			}

			return p_new_rel_item_id_item;
		}

		set
		{
			this.p_new_rel_item_id_item = null;
			this.Item.setPropertyItem("new_rel_id", value.Item);
		}
	}

	ChangeControlledRelationship p_affected_rel_item_id_item;
	public ChangeControlledRelationship AffectedRelItemIdItem
	{
		get
		{
			if (p_affected_rel_item_id_item == null)
			{
				Item affectedRelItem = this.Item.getPropertyItem("affected_rel_id");
				if (affectedRelItem == null)
				{
					return null;
				}

				p_affected_rel_item_id_item = new ChangeControlledRelationship(affectedRelItem);
			}
			return p_affected_rel_item_id_item;
		}

		set
		{
			this.p_affected_rel_item_id_item = null;
			this.Item.setPropertyItem("affected_rel_id", value.Item);
		}
	}
	#endregion

	#region IAffectedRelationshipValidationRules Members

	/// <summary>
	/// All affected relationships on affected items with an item_action of "Release" must have a rel_action of "Attach"
	/// </summary>
	/// <param name="status"></param>
	void IAffectedRelationshipValidationRules.Is_AffectedRelationship_RequiresAttachAction(ResultStatus status)
	{
		ExtendedAffectedItem sourceItem = (ExtendedAffectedItem)this.Relationship.SourceItem;
		if (sourceItem.ItemAction == ExtendedAffectedItem.ItemActionType.Release)
		{
			if (this.Action != RelationshipAction.Attach)
			{
				status.AddError("ValidateAffectedRelationship_Is_AffectedRelationship_RequiresAttachAction");
			}
		}
	}

	/// <summary>
	/// affected_rel is required when rel_action is "Modify" or "Remove"
	/// </summary>
	/// <param name="status"></param>
	void IAffectedRelationshipValidationRules.Is_AffectedRelId_Required(ResultStatus status)
	{
		if (this.Action == RelationshipAction.Modify || this.Action == RelationshipAction.Remove)
		{
			if (this.AffectedRelItemIdItem == null)
			{
				status.AddError("ValidateAffectedRelationship_Is_AffectedRelId_Required", this.Action.ToString());
			}
		}
	}

	/// <summary>
	/// affected_rel relationships must have a source_id that matches the parent affected item's affected_id
	/// </summary>
	/// <param name="status"></param>
	void IAffectedRelationshipValidationRules.Is_AffectedRelId_Required_AffectedId_SourceID(ResultStatus status)
	{
		ExtendedAffectedItem sourceItem = (ExtendedAffectedItem)this.Relationship.SourceItem;
		if (sourceItem.AffectedIdItem == null || this.AffectedRelItemIdItem == null) return;

		if (this.AffectedRelItemIdItem.SourceID != sourceItem.AffectedIdItem.ID)
		{
			status.AddError("ValidateAffectedRelationship_Is_AffectedRelId_Required_AffectedId_SourceID");
		}
	}

	/// <summary>
	/// At least one affected relationship property must be attached when rel_action is "Attach" or "Modify"
	/// </summary>
	/// <param name="status"></param>
	void IAffectedRelationshipValidationRules.Is_AffectedRelationshipProperty_Required(ResultStatus status)
	{
		if (this.Action == RelationshipAction.Attach || this.Action == RelationshipAction.Modify)
		{
			if (this.AffectedRelationshipProperties.Count == 0)
			{
				status.AddError("ValidateAffectedRelationship_Is_AffectedRelationshipProperty_Required", this.Action.ToString());
			}
		}
	}

	void IAffectedRelationshipValidationRules.Verify(ResultStatus status)
	{
		ExtendedAffectedItem ext_affected_item = (ExtendedAffectedItem)this.Relationship.SourceItem;

		if (ext_affected_item.ItemAction != ExtendedAffectedItem.ItemActionType.Release &&
			ext_affected_item.ItemAction != ExtendedAffectedItem.ItemActionType.Revise &&
			ext_affected_item.ItemAction != ExtendedAffectedItem.ItemActionType.Renumber)
		{
			return;
		}

		if (this.Action == RelationshipAction.Attach || this.Action == RelationshipAction.Modify)
		{
			//Verify that new_rel_id populated and new relationship was attached to correct sourceItem and that all properties were
			//modified.

			if (this.NewRelItemIdItem == null)
			{
				status.AddError("VerifyAffectedRelationship_NewRelItemIDIsNull");
				return;
			}

			ChangeControlledRelationship new_relship =
			(from relship in ext_affected_item.NewItemIdItem.Relationships
				where
				relship.ID == this.NewRelItemIdItem.ID
				select relship).SingleOrDefault();

			//if relationship not exists in new_item.
			if (new_relship == null)
			{
				status.AddError("VerifyAffectedRelationship_NewItemNotContainRelationship", this.NewRelItemIdItem.Type, this.NewRelItemIdItem.ID, ext_affected_item.NewItemIdItem.Type, ext_affected_item.NewItemIdItem.ItemNumber);
				return;
			}

			//validate properties which was set 
			foreach (AffectedRelationshipProperty relship_prop in this.AffectedRelationshipProperties)
			{
				string value = new_relship.Item.getProperty(relship_prop.PropertyName);
				if (value != relship_prop.NewValue)
				{
					status.AddError("VerifyAffectedRelationship_NotAllRelationshipPropertiesWereApplied", relship_prop.PropertyName, relship_prop.NewValue, this.Type, this.ID);
				}
			}
		}
		else if (this.Action == RelationshipAction.Remove)
		{
			//Try to find the new relationship and check that it is null because it was deleted.
			ChangeControlledRelationship new_relship =
				(from relship in ext_affected_item.NewItemIdItem.Relationships
					where
					relship.SourceID == ext_affected_item.NewItemIdItem.ID &&
					relship.SortOrder == this.AffectedRelItemIdItem.SortOrder &&
					relship.ItemTypeID == this.AffectedRelItemIdItem.ItemTypeID
					select relship).SingleOrDefault();

			if (new_relship != null)
			{
				//Check if another relationship was added with the same Source ID, Sort Order, and Type
				AffectedRelationship attached_relship =
				(from relship in ext_affected_item.AffectedRelationships
				where
				relship.Action == RelationshipAction.Attach &&
				relship.NewRelItemIdItem.SourceID == ext_affected_item.NewItemIdItem.ID &&
				relship.NewRelItemIdItem.SortOrder == this.AffectedRelItemIdItem.SortOrder &&
				relship.NewRelItemIdItem.ItemTypeID == this.AffectedRelItemIdItem.ItemTypeID
				select relship).SingleOrDefault();
				if (attached_relship == null)
				{
					status.AddError("VerifyAffectedRelationship_RelationshipStillExistInItem", ext_affected_item.NewItemIdItem.Type, ext_affected_item.NewItemIdItem.ItemNumber, new_relship.Type, new_relship.ID);
				}
			}
		}
	}
	#endregion

	#region Members
	/// <summary>
	/// Process affected relationship in depend on Action property.
	/// </summary>
	public void Process()
	{
		ExtendedAffectedItem ext_affected_item = (ExtendedAffectedItem)this.Relationship.SourceItem;

		if (this.Action == RelationshipAction.Attach)
		{
			//Create a new relationship with source_id equal to the new_item_id item on the parent Affected Item 
			//and other properties as specified in Affected Relationship Property relationships.  
			//Populate the new_rel property with the id of the new relationship

			Item new_rel_item = Utils.Innovator.newItem("Part BOM"); // hardcoded, in first implementation iteration we have to do it.
			ChangeControlledRelationship new_relship = new ChangeControlledRelationship(new_rel_item);
			new_relship.SourceID = ext_affected_item.NewItemIdItem.ID;

			//set
			foreach (AffectedRelationshipProperty relship_prop in this.AffectedRelationshipProperties)
			{
				new_relship.Item.setProperty(relship_prop.PropertyName, relship_prop.NewValue);
			}

			new_relship.ApplyAdd();

			this.NewRelItemIdItem = new_relship;
			this.ApplyEdit();
		}
		else if (this.Action == RelationshipAction.Modify)
		{
			//Find the id of the new relationship and set the new_rel property.
			//Edit the new relationship using the properties specified in Affected Relationship Property relationships.

			ChangeControlledRelationship new_relship =
				(from relship in ext_affected_item.NewItemIdItem.Relationships
					where
					relship.SourceID == ext_affected_item.NewItemIdItem.ID &&
					relship.SortOrder == this.AffectedRelItemIdItem.SortOrder &&
					relship.ItemTypeID == this.AffectedRelItemIdItem.ItemTypeID
					select relship).Single();

			this.NewRelItemIdItem = new_relship;
			this.ApplyEdit();

			//set
			foreach (AffectedRelationshipProperty relship_prop in this.AffectedRelationshipProperties)
			{
				this.NewRelItemIdItem.Item.setProperty(relship_prop.PropertyName, relship_prop.NewValue);
			}

			this.NewRelItemIdItem.ApplyEdit();
		}
		else if (this.Action == RelationshipAction.Remove)
		{
			//Find the new relationship and delete it.

			ChangeControlledRelationship new_relship =
				(from relship in ext_affected_item.NewItemIdItem.Relationships
					where
					relship.SourceID == ext_affected_item.NewItemIdItem.ID &&
					relship.SortOrder == this.AffectedRelItemIdItem.SortOrder &&
					relship.ItemTypeID == this.AffectedRelItemIdItem.ItemTypeID
					select relship).Single();

			new_relship.Delete();
		}
	}
	#endregion
}

/// <summary>
/// No related relationship Affected Item Property of Affected Item
/// </summary>
class AffectedItemProperty : NoRelatedRelationshipItem
{
	public AffectedItemProperty()
		: base()
	{
	}

	public AffectedItemProperty(Item item, AffectedItem sourceItem)
		: base(item, sourceItem)
	{
		this.PropertyName = item.getProperty("property_name", "");
		this.NewValue = item.getProperty("new_value", "");
	}

	#region Mapped Properties
	string property_name;
	public string PropertyName
	{
		get { return property_name; }
		private set { property_name = value; }
	}

	string new_value;
	public string NewValue
	{
		get { return new_value; }
		private set { new_value = value; }
	}
	#endregion
}

/// <summary>
/// No related relationship Affected Relationship Property of Affected Relationship
/// </summary>
class AffectedRelationshipProperty : NoRelatedRelationshipItem
{
	public AffectedRelationshipProperty()
		: base()
	{
	}

	public AffectedRelationshipProperty(Item item, AffectedRelationship sourceItem)
		: base(item, sourceItem)
	{
		this.PropertyName = item.getProperty("property_name", "");
		this.NewValue = item.getProperty("new_value", "");
	}

	#region Mapped Properties
	string property_name;
	public string PropertyName
	{
		get { return property_name; }
		private set { property_name = value; }
	}

	string new_value;
	public string NewValue
	{
		get { return new_value; }
		private set { new_value = value; }
	}
	#endregion
}

/// <summary>
/// Create and get access to Innovator object and provide help methods.
/// </summary>
static class Utils
{
	public static void InitializeForRequest(Innovator innovator, Aras.Server.Core.CallContext CCO)
	{
		HttpContext.Current.Items["AffectedItemInnovatorObject"] = innovator;
		HttpContext.Current.Items["AffectedItemInnovatorCCO"] = CCO;
	}

	/// <summary>
	/// Instantiate item with "OK" result
	/// </summary>
	/// <returns>Item with "OK" result</returns>
	public static Item GetOkResult()
	{
		return Utils.Innovator.newResult("OK");
	}

	/// <summary>
	/// Get error item with specific parameters
	/// </summary>
	/// <param name="status">Status with errors</param>
	/// <param name="numberOfDisplayedErrors">The number of displayed errors</param>
	/// <returns>Item with error</returns>
	public static Item GetError(ResultStatus status, int numberOfDisplayedErrors)
	{
		StringBuilder builder = new StringBuilder();

		List<String> lookedupMessages = status.Errors
			.Select((Error error) =>
			{
				return Utils.CCO.ErrorLookup.Lookup(error.name, error.parameters);
			})
			.Distinct()
			.ToList();

		lookedupMessages
			.Where((String message, int index) =>
			{
				return index < numberOfDisplayedErrors;
			})
			.ToList()
			.ForEach((String message) =>
			{
				builder.Append(message);
			});

		if (numberOfDisplayedErrors < lookedupMessages.Count)
		{
			builder.AppendLine("");
			builder.AppendLine("");
			builder.Append(Utils.CCO.ErrorLookup.Lookup("ValidateChangeItem_AdditionalErrors"));
		}

		return Utils.Innovator.newError(builder.ToString());
	}

	/// <summary>
	/// Assert passed item, throw ItemErrorException if item has error
	/// </summary>
	/// <param name="item"></param>
	public static void AssertItem(Item item)
	{
		if (item.isError())
		{
			throw new ItemErrorException(item);
		}
	}

	/// <summary>
	/// Get instance of Innovator object
	/// </summary>
	public static Innovator Innovator
	{
		get
		{
			return ((Innovator)HttpContext.Current.Items["AffectedItemInnovatorObject"]);
		}
	}

	public static Aras.Server.Core.CallContext CCO
	{
		get
		{
			return ((Aras.Server.Core.CallContext)HttpContext.Current.Items["AffectedItemInnovatorCCO"]);
		}
	}

	/// <summary>
	/// Convert string name to enum
	/// </summary>
	/// <typeparam name="T">Enum</typeparam>
	/// <param name="value">enum value</param>
	/// <param name="noneValue">value which will be parsed if value not presented in enum</param>
	/// <returns></returns>
	public static T GetEnumByValue<T>(string value, string noneValue) where T : struct, IConvertible
	{
		Type en = typeof(T);
		if (!en.IsEnum)
		{
			throw new ArgumentException("T must be an enumerated type");
		}

		if (String.IsNullOrEmpty(value))
		{
			return (T)Enum.Parse(en, noneValue);
		}

		return (T)Enum.Parse(en, value);
	}
}

/// <summary>
/// Represent error
/// </summary>
struct Error
{
	public string name;
	public object[] parameters;

	/// <summary>
	/// Create error
	/// </summary>
	/// <param name="name">UserMessage name</param>
	/// <param name="parameters">objects to format error</param>
	public Error(string name, object[] parameters)
	{
		this.name = name;
		this.parameters = parameters;
	}
}

/// <summary>
/// Exception which wrap error Item
/// </summary>
[Serializable]
class ItemErrorException : Exception
{
	public ItemErrorException(Item error)
		: base()
	{
		this.error = error;
	}

	private Item error;
	public Item Error
	{
		get { return error; }
	}
}

/// <summary>
/// Exception which provide status of validation operation.
/// </summary>
[Serializable]
class ValidationException : Exception
{
	public ValidationException(ResultStatus status)
		: base()
	{
		this.status = status;
	}

	private ResultStatus status;
	public ResultStatus ResultStatus
	{
		get { return status; }
	}
}

/// <summary>
/// Type provide ability to agregate multiple errors
/// </summary>
class ResultStatus
{
	private bool p_isSuccess;
	private List<Error> p_errorList = new List<Error>();
	private bool p_throwExceptionIfError;
	private int p_numberOfErrorsBeforeThrowException;

	/// <summary>
	/// Constructor
	/// </summary>
	/// <param name="throwExceptionIfError">if true, then exception will be thrown when error add</param>
	public ResultStatus(bool throwExceptionIfError)
	{
		this.p_isSuccess = true;
		this.p_throwExceptionIfError = throwExceptionIfError;
		this.p_numberOfErrorsBeforeThrowException = 2;
	}

	/// <summary>
	/// Add error to collection. Toggle IsSuccess to false.
	/// </summary>
	/// <param name="name">UserMessage name</param>
	/// <param name="parameters">objects to format error message</param>
	/// <exception cref="ValidationException">throw if constructor get true to throwExceptionIfError</exception>
	public void AddError(string name, params string[] parameters)
	{
		this.p_errorList.Add(new Error(name, parameters));
		this.p_isSuccess = false;

		//throw exception if only if number of available errors were handled
		if (this.p_throwExceptionIfError && this.Errors.Count >= this.p_numberOfErrorsBeforeThrowException)
		{
			throw new ValidationException(this);
		}
	}

	/// <summary>
	/// Get is status success
	/// </summary>
	public bool IsSuccess
	{
		get { return p_isSuccess; }
	}

	/// <summary>
	/// Get Error collection
	/// </summary>
	public IList<Error> Errors
	{
		get { return new System.Collections.ObjectModel.ReadOnlyCollection<Error>(p_errorList); }
	}
}

/// <summary>
/// Base class for relationship item
/// </summary>
abstract class Relationship : ItemContext
{
	public Relationship()
		: base()
	{
	}

	public Relationship(Item item, ItemContext sourceItem)
		: base(item)
	{
		this.sourceItem = sourceItem;
	}

	#region Mapped Properties
	ItemContext sourceItem;
	public ItemContext SourceItem
	{
		get { return sourceItem; }
		protected set { sourceItem = value; }
	}
	#endregion
}

/// <summary>
/// No Related relationship item
/// </summary>
abstract class NoRelatedRelationshipItem : Relationship
{
	public NoRelatedRelationshipItem()
		: base()
	{
	}

	public NoRelatedRelationshipItem(Item item, ItemContext sourceItem)
		: base(item, sourceItem)
	{
	}
}

/// <summary>
/// Represent intermediate related relationship item that link source item with related item.
/// </summary>
class RelatedRelationship : Relationship
{
	public RelatedRelationship(Item item, ItemContext sourceItem)
		: base(item, sourceItem)
	{
	}

	#region Mapped Properties
	public string RelatedID
	{
		get { return this.Item.getProperty("related_id"); }
	}

	public string SortOrder
	{
		get { return this.Item.getProperty("sort_order"); }
	}
	#endregion
}

/// <summary>
/// Related relationship item
/// </summary>
abstract class RelatedRelationshipItem : Relationship
{
	public RelatedRelationshipItem()
		: base()
	{
	}

	public RelatedRelationshipItem(Item item, ItemContext sourceItem)
		: base(item.getItemsByXPath("related_id/Item"), sourceItem)
	{

	}

	private RelatedRelationship relationship;
	/// <summary>
	/// Get relationship item
	/// </summary>
	public RelatedRelationship Relationship
	{
		get
		{
			if (relationship == null)
			{
				relationship = new RelatedRelationship(this.Item.getItemsByXPath("../../Item"), this.SourceItem);
			}
			return relationship;
		}
	}
}

/// <summary>
/// Collection of relationships
/// </summary>
/// <typeparam name="T">Relationship related or no related item</typeparam>
class RelationshipItemList<T> : IList<T>, ICollection<T>, IEnumerable<T>
	where T : Relationship
{
	ItemContext sourceItem;
	Type relationshipPrototypeType;
	T relationshipPrototype;

	/// <summary>
	/// Constructor
	/// </summary>
	/// <param name="relshipName">Relationship name</param>
	/// <param name="prototype">Pass instance of relationship item which will be a prototype of list items. Could be inheritors of base T type.</param>
	/// <param name="sourceItem">Source item</param>
	public RelationshipItemList(string relshipName, T prototype, ItemContext sourceItem)
	{
		this.Name = relshipName;
		this.relationshipPrototype = prototype;
		this.relationshipPrototypeType = this.relationshipPrototype.GetType();
		this.sourceItem = sourceItem;
	}

	public RelationshipItemList(IEnumerable<T> collection)
	{
		list = new List<T>(collection);
		readOnlyList = new System.Collections.ObjectModel.ReadOnlyCollection<T>(list);
	}

	string relshipName;
	public string Name
	{
		get { return relshipName; }
		private set { relshipName = value; }
	}

	private IList<T> list;
	private IList<T> readOnlyList;

	/// <summary>
	/// Bind relationship collection. Attach to ItemContext.Deleted event
	/// </summary>
	public void Bind()
	{
		this.Unbind();
		list = new List<T>();
		readOnlyList = new System.Collections.ObjectModel.ReadOnlyCollection<T>(list);

		Item relshipItems = Utils.Innovator.newItem(this.Name, "get");
		relshipItems.setProperty("source_id", sourceItem.ID);
		relshipItems.setAttribute("serverEvents", "0");

		if (this.Name == "ECN Affected Item" || 
			this.Name == "Simple ECO Affected Item" || 
			this.Name == "Express DCO Affected Item" || 
			this.Name == "Express ECO Affected Item")
		{
			Item affItem = Utils.Innovator.newItem("Affected Item", "get");
			affItem.setAttribute("select", "interchangeable, item_action, action, affected_id, new_item_id");
			affItem.setAttribute("serverEvents", "0");

			Item changeControlledItem = Utils.Innovator.newItem("Change Controlled Item", "get");
			changeControlledItem.setAttribute("serverEvents", "0");
			affItem.setPropertyItem("affected_id", changeControlledItem);
			affItem.setPropertyItem("new_item_id", changeControlledItem);

			relshipItems.setPropertyItem("related_id", affItem);
		}

		if (this.Name == "Affected Item Relationship")
		{
			Item affRelshipItem = Utils.Innovator.newItem("Affected Relationship", "get");
			affRelshipItem.setAttribute("select", "rel_action, affected_rel_id, new_rel_id");
			affRelshipItem.setAttribute("serverEvents", "0");

			Item changeControlledRelship = Utils.Innovator.newItem("Change Controlled Relationship", "get");
			changeControlledRelship.setAttribute("serverEvents", "0");
			changeControlledRelship.setAttribute("related_expand", "0");
			affRelshipItem.setPropertyItem("affected_rel_id", changeControlledRelship);
			affRelshipItem.setPropertyItem("new_rel_id", changeControlledRelship);

			relshipItems.setPropertyItem("related_id", affRelshipItem);
		}

		relshipItems = relshipItems.apply();

		int affItemRelshipCount = relshipItems.getItemCount();

		for (int p = 0; p < affItemRelshipCount; p++)
		{
			Item affItem = relshipItems.getItemByIndex(p);
			T instance = (T)Activator.CreateInstance(this.relationshipPrototypeType, affItem, sourceItem);
			instance.Deleted += new EventHandler(instance_Deleted);			
			list.Add(instance);
		}	
	}

	/// <summary>
	/// Detach from ItemContext.Deleted event
	/// </summary>
	private void Unbind()
	{
		if (list == null) return;
		foreach (T t in list)
		{
			t.Deleted -= new EventHandler(instance_Deleted);
		}
		list.Clear();
		list = null;
	}

	/// <summary>
	/// ItemContext.Deleted event handler. remove item from relationship collection.
	/// </summary>
	/// <param name="sender"></param>
	/// <param name="e"></param>
	private void instance_Deleted(object sender, EventArgs e)
	{
		T instance = (T)sender;
		instance.Deleted -= new EventHandler(instance_Deleted);
		list.Remove(instance);
	}

	/// <summary>
	/// Bind collection if list was not initialized or was unbinded.
	/// </summary>
	private void BindIfNotInitialized()
	{
		if (list == null)
		{
			this.Bind();
		}
	}

	#region IList<T> Members

	public int IndexOf(T item)
	{
		this.BindIfNotInitialized();
		return readOnlyList.IndexOf(item);
	}

	public void Insert(int index, T item)
	{
		this.BindIfNotInitialized();
		readOnlyList.Insert(index, item);
	}

	public void RemoveAt(int index)
	{
		this.BindIfNotInitialized();
		readOnlyList.RemoveAt(index);
	}

	public T this[int index]
	{
		get
		{
			this.BindIfNotInitialized();
			return readOnlyList[index];
		}
		set
		{
			this.BindIfNotInitialized();
			readOnlyList[index] = value;
		}
	}

	#endregion

	#region ICollection<T> Members

	public void Add(T item)
	{
		this.BindIfNotInitialized();
		readOnlyList.Add(item);
	}

	public void Clear()
	{
		this.BindIfNotInitialized();
		readOnlyList.Clear();
	}

	public bool Contains(T item)
	{
		this.BindIfNotInitialized();
		return readOnlyList.Contains(item);
	}

	public void CopyTo(T[] array, int arrayIndex)
	{
		this.BindIfNotInitialized();
		readOnlyList.CopyTo(array, arrayIndex);
	}

	public int Count
	{
		get { this.BindIfNotInitialized(); return readOnlyList.Count; }
	}

	public bool IsReadOnly
	{
		get { this.BindIfNotInitialized(); return readOnlyList.IsReadOnly; }
	}

	public bool Remove(T item)
	{
		this.BindIfNotInitialized();
		return readOnlyList.Remove(item);
	}

	#endregion

	#region IEnumerable<T> Members

	public IEnumerator<T> GetEnumerator()
	{
		this.BindIfNotInitialized();
		return readOnlyList.GetEnumerator();
	}

	#endregion

	#region IEnumerable Members

	System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
	{
		this.BindIfNotInitialized();
		return ((System.Collections.IEnumerable)readOnlyList).GetEnumerator();
	}

	#endregion
}

internal static class ItemContextGroupOperations
{
	public static void Promote<TSource>(this IList<TSource> items, string nextStatus) where TSource : ItemContext
	{
		items.InvokeActionByGroup((groupItems) =>
		{
			groupItems.PromoteImpl(nextStatus);
		});
	}

	public static IList<TSource> Version<TSource>(this IList<TSource> items) where TSource : ItemContext
	{
		TSource[] result = new TSource[items.Count];

		Dictionary<string, int> itemsIndexDict = items
		.Select((templateItem, index) => index)
		.ToDictionary((index) => items[index].ID);

		items.InvokeActionByGroup((groupItems) =>
		{
			IList<TSource> groupItemsResult = groupItems.VersionImpl();

			for (int indexInGroup = 0; indexInGroup < groupItems.Count; indexInGroup++)
			{
				string originalID = groupItems[indexInGroup].ID;
				TSource versionedItem = groupItemsResult[indexInGroup];
				int positionInInputList = itemsIndexDict[originalID];

				result[positionInInputList] = versionedItem;
			}
		});

		return result.ToList();
	}

	public static void Unlock<TSource>(this IList<TSource> items) where TSource : ItemContext
	{
		items.InvokeActionByGroup(UnlockImpl);
	}

	public static void Lock<TSource>(this IList<TSource> items) where TSource : ItemContext
	{
		items.InvokeActionByGroup(LockImpl);
	}

	public static void ApplyUpdate<TSource>(this IList<TSource> items, params string[] updateParameterNames) where TSource : ItemContext
	{	
		if (items.Count() == 0)
		{
			return;
		}

		if (updateParameterNames.Count() > 0)
		{
			string tmpTableName = "PE_45471a4468a3479496638d90b2eefb10";

			Type typeInnDb = Utils.CCO.Variables.InnDatabase.GetType();

			Utils.CCO.Variables.InnDatabase.CreateTable(tmpTableName);

			try
			{

				//Friend MustOverride Sub AddColumn(ByVal tableName As String, ByVal column As InnovatorDBColumn)
				System.Reflection.MethodInfo methodAddColumn = typeInnDb.GetMethod("AddColumn",System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
				System.Reflection.ParameterInfo[] parametersAddColumn = methodAddColumn.GetParameters();
				//Friend Sub New( _
				//ByVal columnName As String, _
				//ByVal columnType As InnovatorDataType)
				System.Reflection.ConstructorInfo ctorInnovatorDBColumn = parametersAddColumn[1].ParameterType.GetConstructors(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)[0];

				foreach (string parameter in updateParameterNames)
				{
					//InnovatorDataType.string_ == 1
					object[] paramsForCtor = new object[]{ parameter, 1 }; 
					object[] paramsForAddColumn = new object[] { tmpTableName, ctorInnovatorDBColumn.Invoke(paramsForCtor)};
					methodAddColumn.Invoke(Utils.CCO.Variables.InnDatabase,paramsForAddColumn);
				}

				using (DataTable dt = new DataTable(tmpTableName))
				{
					dt.Locale = CultureInfo.InvariantCulture;

					dt.Columns.Add("id");
					foreach (string parameter in updateParameterNames)
					{
						dt.Columns.Add(parameter);
					}

					foreach (TSource item in items)
					{
						DataRow row = dt.NewRow();
						row[0] = item.Item.getID();
						
						for (int i = 0; i < updateParameterNames.Count(); i++)
						{
							row[i + 1] = item.Item.getProperty(updateParameterNames[i]);
						}
						
						dt.Rows.Add(row);
					}

					System.Data.SqlClient.SqlConnection sqlConnection = (System.Data.SqlClient.SqlConnection)(typeInnDb.GetProperty("CurrentConnection",System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(Utils.CCO.Variables.InnDatabase,null));
					System.Data.SqlClient.SqlTransaction sqlTransaction = (System.Data.SqlClient.SqlTransaction)(typeInnDb.GetProperty("CurrentTransaction",System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(Utils.CCO.Variables.InnDatabase,null));

					using (System.Data.SqlClient.SqlBulkCopy sqlBulkCopy = new System.Data.SqlClient.SqlBulkCopy(sqlConnection, System.Data.SqlClient.SqlBulkCopyOptions.Default, sqlTransaction))
					{
						sqlBulkCopy.DestinationTableName = tmpTableName;
						sqlBulkCopy.WriteToServer(dt);
					}
				}

				string type = items.First().Type;
				string typeTableName = Utils.CCO.DB.GenerateTableName(type);
				string updateQueryFormat = "UPDATE " + typeTableName + " SET {0} FROM " + tmpTableName + " WHERE " + tmpTableName + ".ID = " + typeTableName + ".ID";
				string setColumnFormat = "{0} = " + tmpTableName + ".{0}";

				StringBuilder setColumns = new StringBuilder();
				foreach (string parameter in updateParameterNames)
				{
					setColumns.AppendFormat(setColumnFormat,parameter);
					setColumns.Append(",");
				}
				setColumns.Remove(setColumns.Length - 1, 1);

				string sqlQuery = string.Format(updateQueryFormat,setColumns);
			
				Aras.Server.Core.InnovatorDatabase conn = Utils.CCO.Variables.InnDatabase;
				conn.ExecuteSQL(sqlQuery);
			}
			finally
			{
				if (Utils.CCO.Variables.InnDatabase.TableExists(tmpTableName))
				{
					Utils.CCO.Variables.InnDatabase.DropTable(tmpTableName);
				}
			}
		}
		
		StringBuilder idlist = new StringBuilder();
		idlist.Append(items.First().Item.getID());
		for (int i = 1; i < items.Count(); i++)
		{
			idlist.Append(",");
			idlist.Append(items.ElementAt(i).Item.getID());
		}

		string amlFormat = "<AML><Item type='{1}' action='update' idlist='{0}' doGetItem='0' version='0'></Item></AML>";
		string aml = string.Format(amlFormat,idlist.ToString(),items.First().Type);
		Item result = Utils.Innovator.applyAML(aml.ToString());
		Utils.AssertItem(result);
	}

	//TODO: innovator doesn't support group copy yet
	public static IList<TSource> Copy<TSource>(this IList<TSource> items) where TSource : ItemContext
	{
		if (items.Count() == 0)
		{
			return items;
		}

		List<TSource> result = new List<TSource>();
		foreach (TSource item in items)
		{
			TSource resItem = item.ApplyCopy<TSource>();
			result.Add(resItem);
		}

		return result;
	}

	public static void ProcessAffectedRelationships<TSource>(this IList<TSource> affectedItems) where TSource : ExtendedAffectedItem
	{
		foreach (TSource extAffectedItem in affectedItems)
		{
			extAffectedItem.ProcessAffectedRelationships();
		}
	}

	public static void ProcessAffectedItemProperties<TSource>(this IList<TSource> affectedItems) where TSource : ExtendedAffectedItem
	{
		List<String> propertyListNames = new List<String>();

		IList<ChangeControlledItem> newItemIdItemList =
				affectedItems.Select(
						(TSource affectedItem) =>
						{
							foreach (AffectedItemProperty affectedItemProperty in affectedItem.AffectedItemProperties)
							{
								affectedItem.NewItemIdItem.Item.setProperty(affectedItemProperty.PropertyName, affectedItemProperty.NewValue);

								if (!propertyListNames.Contains(affectedItemProperty.PropertyName))
								{
									propertyListNames.Add(affectedItemProperty.PropertyName);
								}
							}
							return affectedItem.NewItemIdItem;
						}
				)
				.ToList();

		newItemIdItemList.Lock();
		newItemIdItemList.ApplyUpdate(propertyListNames.ToArray());
		newItemIdItemList.Unlock();
	}

	#region Action implementations
	private static void PromoteImpl<TSource>(this IList<TSource> items, string nextStatus) where TSource : ItemContext
	{
		if (items.Count() == 0)
		{
			return;
		}

		String[] promoteIds = items.Select((TSource item) => item.ID).ToArray();
		Item promoteItem = Utils.Innovator.newItem(items.First().Type);
		promoteItem.setAttribute("idlist", String.Join(",", promoteIds));

		Item res = promoteItem.promote(nextStatus, "Promote set");

		Utils.AssertItem(res);
	}

	private static IList<TSource> VersionImpl<TSource>(this IList<TSource> items) where TSource : ItemContext
	{
		if (items.Count() == 0)
		{
			return items;
		}

		String[] versionIds = items.Select((TSource item) => item.ID).ToArray();
		String type = items.First().Type;
		Item versionItem = Utils.Innovator.newItem(type, "version");

		String idList = String.Join(",", versionIds);
		versionItem.setAttribute("idlist", idList);

		Item res = versionItem.apply();
		Utils.AssertItem(res);

		StringBuilder updateSql = new StringBuilder();
		updateSql.AppendFormat("UPDATE {0} SET EFFECTIVE_DATE = NULL WHERE ID IN (", Utils.CCO.DB.InnDatabase.GetTableName(type));

		List<TSource> result = new List<TSource>();
		int versionedItemCount = res.getItemCount();

		for (int i = 0; i < versionedItemCount; i++)
		{
			TSource item = (TSource)Activator.CreateInstance(typeof(TSource), res.getItemByIndex(i));
			result.Add(item);

			updateSql.AppendFormat("'{0}'", item.ID);

			if (i != versionedItemCount - 1)
			{
				updateSql.Append(",");
			}
		}

		updateSql.Append(")");

		Utils.CCO.DB.InnDatabase.ExecuteSQL(updateSql.ToString());

		return result;
	}

	private static void UnlockImpl<TSource>(this IList<TSource> items) where TSource : ItemContext
	{
		if (items.Count() == 0)
		{
			return;
		}

		String[] unlockIds = items.Select((TSource item) => item.ID).ToArray();
		Item unlockItem = Utils.Innovator.newItem(items.First().Type, "unlock");
		unlockItem.setAttribute("idlist", String.Join(",", unlockIds));

		Item res = unlockItem.apply();

		Utils.AssertItem(res);
	}

	private static void LockImpl<TSource>(this IList<TSource> items) where TSource : ItemContext
	{
		if (items.Count() == 0)
		{
			return;
		}

		String[] lockIds = items.Select((TSource item) => item.ID).ToArray();
		Item lockItem = Utils.Innovator.newItem(items.First().Type, "lock");
		lockItem.setAttribute("idlist", String.Join(",", lockIds));

		Item res = lockItem.apply();
		Utils.AssertItem(res);
	}

	#endregion

	private static void InvokeActionByGroup<TSource>(this IList<TSource> items, Action<IList<TSource>> operationAction) where TSource : ItemContext
	{
		List<List<TSource>> itemGroups = items
				.GroupBy((item) =>
				{
					return item.Type;
				}, (item) =>
				{
					return item;
				})
				.Select((groupedItems) =>
				{
					return groupedItems.ToList();
				}).ToList();

		foreach (var itemGroup in itemGroups)
		{
			operationAction(itemGroup);
		}
	}
}

class fin
{
	void method()
	{