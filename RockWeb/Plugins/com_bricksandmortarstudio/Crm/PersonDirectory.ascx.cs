using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Web.UI.WebControls;
using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Lava;
using Rock.Model;
using Rock.Web.Cache;
using Rock.Web.UI.Controls;

namespace com_bricksandmortarstudio.Crm
{
    /// <summary>
    /// A directory of people in database.
    /// </summary>
    [DisplayName( "Person Directory" )]
    [Category( "Bricks and Mortar Studio" )]
    [Description( "A directory of people in database." )]

    [DataViewField( "Data View",
        "The data view to use as the source for the directory. Only those people returned by the data view filter will be displayed on this directory.",
        true, "cb4bb264-a1f4-4edb-908f-2ccf3a534bc7", "Rock.Model.Person", "", 0 )]
    [GroupField( "Opt-out Group", "A group that contains people that should be excluded from this list.", false, "", "", 1, "OptOut" )]
    [CustomRadioListField( "Show By", "People can be displayed individually, or grouped by family", "Individual,Family", true, "Individual", "", 2 )]
    [BooleanField( "Show All People", "Display all people by default? If false, a search is required first, and only those matching search criteria will be displayed.", false, "", 3 )]
    [IntegerField( "First Name Characters Required", "The number of characters that need to be entered before allowing a search.", false, 1, "", 4 )]
    [IntegerField( "Last Name Characters Required", "The number of characters that need to be entered before allowing a search.", false, 3, "", 5 )]
    [IntegerField( "Max Results", "The maximum number of results to show on the page.", true, 600 )]
    [CodeEditorField( "Family Lava Template", "The lava template to use to display content for a family member", defaultValue:@"{% for definedValue in DefinedValues %}
    {{ definedValue.Value }}
{% endfor %}", order:4, key:"FamilyLava" )]
    [CodeEditorField( "Person Lava Template", "The lava template to use to display content for a person", defaultValue:@"{% for definedValue in DefinedValues %}
    {{ definedValue.Value }}{% endfor %}", order:4, key:"PersonLava" )]
    [LavaCommandsField( "Enabled Lava Commands", "The Lava commands that should be enabled for the directory.", false, order: 2 )]

    public partial class PersonDirectory : Rock.Web.UI.RockBlock
    {
        #region Fields

        private Guid? _dataViewGuid = null;
        private Guid? _optOutGroupGuid = null;
        private bool _showFamily = false;
        private bool _showAllPeople = false;
        private int? _firsNameCharsRequired = 1;
        private int? _lastNameCharsRequired = 3;
        private int _maxResults = 1000;
        private string _enabledLavaCommands = string.Empty;
        private string _familyLava;
        private string _personLava;
        
        #endregion

        #region Properties
        #endregion

        #region Base Control Methods

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Init" /> event.
        /// </summary>
        /// <param name="e">An <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnInit( EventArgs e )
        {
            base.OnInit( e );

            RockPage.AddScriptLink( ResolveRockUrl( "~/Scripts/jquery.lazyload.min.js" ) );

            // this event gets fired after block settings are updated. it's nice to repaint the screen if these settings would alter it
            this.BlockUpdated += Block_BlockUpdated;
            this.AddConfigurationUpdateTrigger( upnlContent );

            _dataViewGuid = GetAttributeValue( "DataView" ).AsGuidOrNull();
            _optOutGroupGuid = GetAttributeValue( "OptOut" ).AsGuidOrNull();
            _showFamily = GetAttributeValue( "ShowBy" ) == "Family";
            _showAllPeople = GetAttributeValue( "ShowAllPeople" ).AsBoolean();
            _firsNameCharsRequired = GetAttributeValue( "FirstNameCharactersRequired" ).AsIntegerOrNull();
            _lastNameCharsRequired = GetAttributeValue( "LastNameCharactersRequired" ).AsIntegerOrNull();
            _personLava = GetAttributeValue( "PersonLava" );
            _familyLava = GetAttributeValue( "FamilyLava" );
            _maxResults = GetAttributeValue("MaxResults").AsInteger();
            _enabledLavaCommands = GetAttributeValue("EnabledLavaCommands");

            phLetters.Visible = _showAllPeople;

            lbOptInOut.Visible = CurrentPerson != null && _optOutGroupGuid.HasValue;

            BuildLetters();
        }

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Load" /> event.
        /// </summary>
        /// <param name="e">The <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnLoad( EventArgs e )
        {
            nbValidation.Visible = false;

            base.OnLoad( e );

            if ( !Page.IsPostBack )
            {
                BindData();
            }
        }

        #endregion

        #region Events

        // handlers called by the controls on your block

        /// <summary>
        /// Handles the BlockUpdated event of the control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void Block_BlockUpdated( object sender, EventArgs e )
        {
            BindData();
        }

        protected void lbSearch_Click( object sender, EventArgs e )
        {
            var msgs = new List<string>();

            if ( _firsNameCharsRequired.HasValue && tbFirstName.Text.Trim().Length < _firsNameCharsRequired.Value )
            {
                msgs.Add( string.Format( "Please enter at least {0:N0} {1} in First Name before searching.",
                    _firsNameCharsRequired.Value, "character".PluralizeIf( _firsNameCharsRequired.Value > 1 ) ) );
            }

            if ( _lastNameCharsRequired.HasValue && tbLastName.Text.Trim().Length < _lastNameCharsRequired.Value )
            {
                msgs.Add( string.Format( "Please enter at least {0:N0} {1} in Last Name before searching.",
                    _lastNameCharsRequired.Value, "character".PluralizeIf( _lastNameCharsRequired.Value > 1 ) ) );
            }

            if ( msgs.Any() )
            {
                ShowMessages( msgs );
            }
            else
            {
                BindData();
            }
        }

        protected void lbClear_Click( object sender, EventArgs e )
        {
            tbFirstName.Text = string.Empty;
            tbLastName.Text = string.Empty;

            BindData();
        }

        private void lbLetter_Click( object sender, EventArgs e )
        {
            var lbLetter = sender as LinkButton;
            if ( lbLetter != null )
            {
                tbFirstName.Text = string.Empty;
                tbLastName.Text = lbLetter.ID.Substring( 8 );
            }

            BindData();
        }

        protected void btnOptOutIn_Click( object sender, EventArgs e )
        {
            if ( CurrentPerson != null && _optOutGroupGuid.HasValue )
            {
                using ( var rockContext = new RockContext() )
                {
                    bool optIn = lbOptInOut.Text == "Opt in to the Directory";
                    if ( _showFamily )
                    {
                        var familyGroupType = GroupTypeCache.Read( Rock.SystemGuid.GroupType.GROUPTYPE_FAMILY.AsGuid() );

                        foreach ( var familyMember in new GroupService( rockContext )
                            .Queryable().AsNoTracking()
                            .Where( g =>
                                g.GroupTypeId == familyGroupType.Id &&
                                g.Members.Any( m => m.PersonId == CurrentPerson.Id ) )
                            .SelectMany( g => g.Members )
                            .Select( m => m.Person ) )
                        {
                            OptInOutPerson( rockContext, CurrentPerson, optIn );
                        }
                    }
                    else
                    {
                        OptInOutPerson( rockContext, CurrentPerson, optIn );
                    }

                    rockContext.SaveChanges();
                }

                BindData();
            }
        }

        #endregion

        #region Methods

        private void BuildLetters()
        {
            for ( char c = 'A'; c <= 'Z'; c++ )
            {
                LinkButton lbLetter = new LinkButton();
                lbLetter.ID = string.Format( "lbLetter{0}", c );
                lbLetter.Text = c.ToString();
                lbLetter.Click += lbLetter_Click;

                var li = new System.Web.UI.HtmlControls.HtmlGenericControl( "li" );
                li.Controls.Add( lbLetter );

                phLetters.Controls.Add( li );
            }
        }

        private void BindData()
        {
            using ( var rockContext = new RockContext() )
            {
                var dataView = new DataViewService( rockContext ).Get( _dataViewGuid ?? Guid.Empty );
                if ( dataView != null )
                {
                    var personService = new PersonService( rockContext );

                    // Filter people by dataview
                    var errorMessages = new List<string>();
                    var paramExpression = personService.ParameterExpression;
                    var whereExpression = dataView.GetExpression( personService, paramExpression, out errorMessages );
                    var personQry = personService
                        .Queryable( false, false ).AsNoTracking()
                        .Where( paramExpression, whereExpression, null );

                    var dvPersonIdQry = personQry.Select( p => p.Id );

                    bool filteredQry = false;

                    // Filter by first name
                    string firstName = tbFirstName.Text.Trim();
                    if ( !string.IsNullOrWhiteSpace( firstName ) )
                    {
                        personQry = personQry.Where( p =>
                            p.FirstName.StartsWith( firstName ) ||
                            p.NickName.StartsWith( firstName ) );
                        filteredQry = true;
                    }

                    // Filter by last name
                    string lastName = tbLastName.Text.Trim();
                    if ( !string.IsNullOrWhiteSpace( lastName ) )
                    {
                        personQry = personQry.Where( p =>
                            p.LastName.StartsWith( lastName ) );
                        filteredQry = true;
                    }


                    if ( filteredQry || _showAllPeople )
                    {
                        if ( _optOutGroupGuid.HasValue )
                        {
                            var optOutPersonIdQry = new GroupMemberService( rockContext )
                                .Queryable().AsNoTracking()
                                .Where( g => g.Group.Guid.Equals( _optOutGroupGuid.Value ) )
                                .Select( g => g.PersonId );

                            personQry = personQry.Where( p =>
                                !optOutPersonIdQry.Contains( p.Id ) );
                        }

                        if ( _showFamily )
                        {
                            RenderFamilies( rockContext, personQry, dvPersonIdQry );
                        }
                        else
                        {
                            RenderPeople(personQry );
                        }

                    }
                }
                else
                {
                    ShowMessages( new List<string> { "This block requires a valid Data View setting." } );
                }

                if ( CurrentPerson != null && _optOutGroupGuid.HasValue )
                {
                    bool optedOut = new GroupMemberService( rockContext )
                            .Queryable().AsNoTracking()
                            .Any( m =>
                                m.PersonId == CurrentPerson.Id &&
                                m.Group.Guid.Equals( _optOutGroupGuid.Value ) );
                    lbOptInOut.Text = optedOut ? "Opt in to the Directory" : "Opt Out of the Directory";
                }
            }

        }

        private void RenderPeople(IQueryable<Person> personQry )
        {
             var people = personQry
                .OrderBy( p => p.LastName )
                .ThenBy( p => p.NickName )
                .Take( _maxResults );

            var stopwatch = new Stopwatch();
            var stringBuilder = new StringBuilder();

            stopwatch.Start();
            var mergeFields = LavaHelper.GetCommonMergeFields( null, null, new CommonMergeFieldsOptions { GetLegacyGlobalMergeFields  = false} );
            foreach (var person in people)
            {
                mergeFields.Add("Person", person);
                stringBuilder.Append(_personLava.ResolveMergeFields(mergeFields));
                mergeFields.Remove("Person");
            }
            stopwatch.Stop();
            lLava.Text = stringBuilder.ToString();
        }

        private void RenderFamilies( RockContext rockContext, IQueryable<Person> personQry, IQueryable<int> dataviewPersonIdQry )
        {
            var familyGroupType = GroupTypeCache.Read( Rock.SystemGuid.GroupType.GROUPTYPE_FAMILY.AsGuid() );

            var personIds = personQry.Select( p => p.Id );

            var groupMemberQry = new GroupMemberService( rockContext )
                .Queryable().AsNoTracking()
                .Where( m =>
                    m.Group.GroupTypeId == familyGroupType.Id &&
                    personIds.Contains( m.PersonId ) );

            var familyIdQry = groupMemberQry.Select( m => m.GroupId ).Distinct();

            var groupService = new GroupService(rockContext);

            var familyMembers = groupService
                .Queryable().AsNoTracking()
                .Where(g => familyIdQry.Contains(g.Id))
                .Select(g => new
                {
                    Family = g,
                    People = g.Members
                        .Where(m => dataviewPersonIdQry.Contains(m.PersonId))
                        .OrderBy(m => m.GroupRole.Order)
                        .ThenBy(m => m.Person.BirthDate)
                        .Select(m => m.Person)
                        .ToList()
                })
            .Take( GetAttributeValue( "MaxResults" ).AsInteger() );

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var stringBuilder = new StringBuilder();
            foreach ( var familyGrouping in familyMembers)
            {
                var mergeFields = LavaHelper.GetCommonMergeFields( null );
                mergeFields.Add("People", familyGrouping.People);
                mergeFields.Add( "Family", familyGrouping.Family );
                stringBuilder.Append( _familyLava.ResolveMergeFields( mergeFields, _enabledLavaCommands ) );
            }
            stopwatch.Stop();
            lLava.Text = stringBuilder.ToString();
        }

        private void OptInOutPerson( RockContext rockContext, Person person, bool optIn )
        {
            var groupMemberService = new GroupMemberService( rockContext );
            var groupMembers = groupMemberService
                .Queryable()
                .Where( m =>
                    m.PersonId == person.Id &&
                    m.Group.Guid.Equals( _optOutGroupGuid.Value ) )
                .ToList();

            if ( !optIn && !groupMembers.Any() )
            {
                var optOutGroup = new GroupService( rockContext ).Get( _optOutGroupGuid.Value );
                if ( optOutGroup != null )
                {
                    var groupMember = new GroupMember();
                    groupMember.GroupId = optOutGroup.Id;
                    groupMember.PersonId = person.Id;
                    groupMember.GroupRoleId = optOutGroup.GroupType.DefaultGroupRoleId ?? 0;
                    optOutGroup.Members.Add( groupMember );
                }
            }

            if ( optIn && groupMembers.Any() )
            {
                foreach ( var groupMember in groupMembers )
                {
                    groupMemberService.Delete( groupMember );
                }
            }
        }

        private void ShowMessages( List<string> messages )
        {
            if ( messages.Any() )
            {
                nbValidation.Text = string.Format( "Please correct the following <ul><li>{0}</li></ul>",
                    messages.AsDelimited( "</li><li>" ) );
                nbValidation.Visible = true;
            }
        }

        #endregion
    }
}