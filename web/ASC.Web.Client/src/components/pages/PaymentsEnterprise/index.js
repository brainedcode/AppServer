import React, { useEffect } from "react";
import { PageLayout, utils, Error404 } from "asc-web-common";
import { useTranslation } from "react-i18next";

import { Loader, utils as Utils } from "asc-web-components";
import styled from "styled-components";
import { withRouter } from "react-router";
import { connect } from "react-redux";

import PropTypes from "prop-types";
import HeaderContainer from "./sub-components/header-container";
import AdvantagesContainer from "./sub-components/advantages-container";
import ButtonContainer from "./sub-components/button-container";
import ContactContainer from "./sub-components/contact-container";
import { setLicense } from "../../../store/payments/actions";
import { resetLicenseUploaded } from "../../../store/wizard/actions";
import { createI18N } from "../../../helpers/i18n";

const i18n = createI18N({
  page: "PaymentsEnterprise",
  localesPath: "pages/PaymentsEnterprise",
});

const { changeLanguage } = utils;
const { tablet, mobile } = Utils.device;

const StyledBody = styled.div`
  margin: 0 auto;
  width: 920px;

  @media ${tablet} {
    width: 600px;
    margin: 0 auto;
  }
  @media ${mobile} {
    width: 343px;
    margin: 0 auto;
  }
`;

const Body = ({
  isLoaded,
  salesEmail,
  helpUrl,
  buyUrl,
  dateExpires,
  hasErrorLicense,
  licenseUpload,
  wizardToken,
}) => {
  const { t } = useTranslation("translation", { i18n });
  useEffect(() => {
    changeLanguage(i18n);
    document.title = `${t("Payments")}`;
  }, [t]);

  return !isLoaded ? (
    <Loader className="pageLoader" type="rombs" size="40px" />
  ) : (
    <StyledBody>
      <HeaderContainer t={t} dateExpires={dateExpires} />
      <AdvantagesContainer t={t} />
      <ButtonContainer
        t={t}
        buyUrl={buyUrl}
        hasErrorLicense={hasErrorLicense}
      />
      <ContactContainer t={t} salesEmail={salesEmail} helpUrl={helpUrl} />
    </StyledBody>
  );
};

const PaymentsEnterprise = (props) => (
  <PageLayout>
    <PageLayout.SectionBody>
      <Body {...props} />
    </PageLayout.SectionBody>
  </PageLayout>
);

PaymentsEnterprise.propTypes = {
  standAloneMode: PropTypes.bool,
  isLoaded: PropTypes.bool,
};

function mapStateToProps(state) {
  return {
    isLoaded: state.auth.isLoaded,
    salesEmail: state.payments.salesEmail,
    helpUrl: state.payments.helpUrl,
    buyUrl: state.payments.buyUrl,
    dateExpires: state.payments.dateExpires,
    licenseUpload: state.wizard.licenseUpload,
    wizardToken: state.payments.wizardToken,
    hasErrorLicense: state.payments.hasErrorLicense,
  };
}
export default connect(mapStateToProps, {
  setLicense,
  resetLicenseUploaded,
})(withRouter(PaymentsEnterprise));
