import React from 'react';
import { withRouter } from "react-router";
import { withTranslation } from 'react-i18next';
import { PageLayout, Loader } from 'asc-web-components';
import { connect } from 'react-redux';
import PropTypes from 'prop-types';
import { store } from 'asc-web-common';
const { logout, changeEmail } = store.auth.actions;

class ActivateEmail extends React.PureComponent {

    componentDidMount() {
        const { history, logout, changeEmail, linkData } = this.props;
        const [email, uid, key] = [linkData.email, linkData.uid, linkData.confirmHeader];
        logout();
        changeEmail(uid, email, key)
            .then((res) => {
                history.push(`/login/confirmed-email=${email}`);
            })
            .catch((e) => {
                console.log('activate email error', e);
                history.push(`/login/error=${e}`);
            });
    }

    render() {
        console.log('Activate email render');
        return (
            <Loader className="pageLoader" type="rombs" size={40} />
        );
    }
}



ActivateEmail.propTypes = {
    location: PropTypes.object.isRequired,
    history: PropTypes.object.isRequired
};
const ActivateEmailForm = (props) => (<PageLayout sectionBodyContent={<ActivateEmail {...props} />} />);


export default connect(null, { logout, changeEmail })(withRouter(withTranslation()(ActivateEmailForm)));