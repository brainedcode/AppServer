// import {} from "./actions";

const initialState = {
  wizardToken: null,
  salesEmail: "sales@onlyoffice.com",
  helpUrl: "https://helpdesk.onlyoffice.com",
  buyUrl: "http://www.onlyoffice.com/post.ashx?type=buyenterprise",
  standAloneMode: true,
  dateExpires: "1 марта 2020",
};

const paymentsReducer = (state = initialState, action) => {
  switch (action.type) {
    // case
    default:
      return state;
  }
};

export default paymentsReducer;
