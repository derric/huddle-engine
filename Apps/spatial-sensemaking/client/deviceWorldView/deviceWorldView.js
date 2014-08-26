Template.deviceWorldView.rendered = function() {
  Template.deviceWorldView.hide(false);
};

Template.deviceWorldView.deviceBorderColorCSS = function() {
  var info = DeviceInfo.findOne({ _id: this.id });
  if (info === undefined || !info.color) return "";

  return 'border-color: rgb('+info.color.r+', '+info.color.g+', '+info.color.b+');';
};

Template.deviceWorldView.deviceSizeAndPosition = function() {
  var width = $("#worldViewWrapper").width() / this.ratio.x;
  var height = $("#worldViewWrapper").height() / this.ratio.y;
  var x = ($("#worldViewWrapper").width() - width) * this.topLeft.x;
  var y = ($("#worldViewWrapper").height() - height) * this.topLeft.y;
  return 'width: '+width+'px; height: '+height+'px; top: '+y+'px; left: '+x+'px;';
};

Template.deviceWorldView.thisDevice = function() {
  return Session.get("thisDevice") || undefined;
};

Template.deviceWorldView.otherDevices = function() {
  return Session.get("otherDevices") || [];
};

//
// "PUBLIC" METHODS
//

Template.deviceWorldView.show = function(animated) {
  if (animated === undefined) animated = true;

  var duration = animated ? 1000 : 0;
  $("#worldViewWrapper").animate({
    top: "0px"
  }, duration);
};

Template.deviceWorldView.hide = function(animated) {
  if (animated === undefined) animated = true;

  var duration = animated ? 1000 : 0;
  $("#worldViewWrapper").animate({
    top: $(document).height()+"px"
  }, duration);
};

//
// EVENTS
//

Template.deviceWorldView.events({
  'click #closeButton, touchdown #closeButton': function() {
    Template.deviceWorldView.hide();
  }
});